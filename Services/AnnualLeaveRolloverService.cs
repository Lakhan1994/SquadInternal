using System.Data;
using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Models;

namespace SquadInternal.Services
{
    public class AnnualLeaveRolloverService
    {
        private readonly AppDbContext _db;
        private readonly AnnualLeavePolicyService _policyService;

        public AnnualLeaveRolloverService(AppDbContext db, AnnualLeavePolicyService policyService)
        {
            _db = db;
            _policyService = policyService;
        }

        public async Task ProcessYearAsync(int leaveYear, int? actorUserId, CancellationToken cancellationToken = default)
        {
            if (leaveYear < 2027 || leaveYear > DateTime.UtcNow.Year)
                throw new InvalidOperationException("Rollover can only process an effective leave year that has started.");

            var execution = await _db.LeaveRolloverExecutions.FirstOrDefaultAsync(
                item => item.LeaveYear == leaveYear, cancellationToken);
            if (execution?.Status == "Completed")
                return;

            if (execution == null)
            {
                execution = new LeaveRolloverExecution
                {
                    LeaveYear = leaveYear,
                    Status = "Started",
                    StartedOn = DateTime.UtcNow,
                    StartedByUserId = actorUserId
                };
                _db.LeaveRolloverExecutions.Add(execution);
            }
            else
            {
                execution.Status = "Started";
                execution.Error = null;
                execution.StartedOn = DateTime.UtcNow;
                execution.StartedByUserId = actorUserId;
            }
            await _db.SaveChangesAsync(cancellationToken);

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                execution = await _db.LeaveRolloverExecutions.FirstAsync(
                    item => item.LeaveYear == leaveYear, cancellationToken);
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var policy = await _policyService.GetPolicyAsync(leaveYear, cancellationToken)
                        ?? throw new InvalidOperationException("No leave policy is configured for this year.");
                    var employees = await _db.Employees
                        .Where(employee => employee.IsActive &&
                            (!employee.DateOfJoining.HasValue || employee.DateOfJoining.Value.Date <= new DateTime(leaveYear, 12, 31)))
                        .ToListAsync(cancellationToken);
                    var categories = new List<string> { "Casual Leave", "Sick Leave" };
                    if (policy.EarnedLeaveEnabled)
                        categories.Add("Earned Leave");

                    foreach (var employee in employees)
                    {
                        decimal employeeBonusDays = 0m;
                        foreach (var category in categories)
                        {
                            var current = await _policyService.GetOrCreateBalanceAsync(employee, leaveYear, category, cancellationToken)
                                ?? throw new InvalidOperationException($"Unable to initialize {category} for employee {employee.Id}.");
                            var prior = await _db.EmployeeLeaveBalances.FirstOrDefaultAsync(
                                item => item.EmployeeId == employee.Id && item.LeaveYear == leaveYear - 1 && item.LeaveType == category,
                                cancellationToken);

                            if (policy.CarryForwardUnusedLeave && prior != null)
                            {
                                var carryKey = $"carry-forward:{leaveYear - 1}:{leaveYear}:{employee.Id}:{category}";
                                if (!await _db.LeaveBalanceTransactions.AnyAsync(item => item.IdempotencyKey == carryKey, cancellationToken))
                                {
                                    var carriedDays = Math.Max(0m, prior.ClosingBalance);
                                    current.OpeningBalance += carriedDays;
                                    current.ClosingBalance += carriedDays;
                                    AddTransaction(employee.Id, leaveYear, category, "CarryForward", carriedDays, carryKey,
                                        $"Unused balance from {leaveYear - 1}", actorUserId);
                                }
                            }

                            if (prior == null)
                                continue;

                            var bonusDays = CalculateBonus(policy, category, prior.ClosingBalance, employeeBonusDays);
                            if (bonusDays <= 0m)
                                continue;

                            var targetCategory = policy.BonusUsesSeparateCategory ? "Bonus Leave" : category;
                            var target = targetCategory == category
                                ? current
                                : await GetOrCreateBonusBalanceAsync(employee.Id, leaveYear, targetCategory, cancellationToken);
                            var bonusKey = policy.BonusRequiresApproval
                                ? $"year-end-bonus-pending:{leaveYear - 1}:{leaveYear}:{employee.Id}:{category}"
                                : $"year-end-bonus:{leaveYear - 1}:{leaveYear}:{employee.Id}:{category}";
                            if (await _db.LeaveBalanceTransactions.AnyAsync(item => item.IdempotencyKey == bonusKey, cancellationToken))
                                continue;

                            employeeBonusDays += bonusDays;
                            var bonusTransaction = AddTransaction(employee.Id, leaveYear, targetCategory,
                                policy.BonusRequiresApproval ? "BonusPending" : "BonusCredit", bonusDays, bonusKey,
                                $"Eligible unused {category} balance from {leaveYear - 1}", actorUserId);
                            if (!policy.BonusRequiresApproval)
                            {
                                target.BonusCredits += bonusDays;
                                target.ClosingBalance += bonusDays;
                                target.UpdatedOn = DateTime.UtcNow;
                            }
                            if (policy.BonusExpirationMonths.HasValue)
                                bonusTransaction.ExpiresOn =
                                    new DateTime(leaveYear, 1, 1).AddMonths(policy.BonusExpirationMonths.Value);
                        }
                    }

                    execution.Status = "Completed";
                    execution.CompletedOn = DateTime.UtcNow;
                    execution.Error = null;
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    var failedExecution = await _db.LeaveRolloverExecutions.FirstAsync(
                        item => item.LeaveYear == leaveYear, cancellationToken);
                    failedExecution.Status = "Failed";
                    failedExecution.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                    await _db.SaveChangesAsync(cancellationToken);
                    throw;
                }
            });
        }

        public async Task ExpireDueBonusesAsync(CancellationToken cancellationToken = default)
        {
            var dueBalances = await _db.LeaveBalanceTransactions
                .Where(item => item.TransactionType == "BonusCredit" && item.ExpiresOn.HasValue &&
                    item.ExpiresOn.Value <= DateTime.UtcNow)
                .Select(item => new { item.EmployeeId, item.LeaveYear, item.LeaveType })
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var due in dueBalances)
            {
                var balance = await _db.EmployeeLeaveBalances.FirstOrDefaultAsync(item =>
                    item.EmployeeId == due.EmployeeId && item.LeaveYear == due.LeaveYear && item.LeaveType == due.LeaveType,
                    cancellationToken);
                if (balance != null)
                    await _policyService.ExpireBonusCreditsAsync(balance, cancellationToken);
            }
        }

        public static decimal CalculateBonus(AnnualLeavePolicy policy, string category, decimal unusedDays, decimal alreadyAwarded)
        {
            var qualifies = category == "Casual Leave"
                ? policy.CasualLeaveBonusEligible
                : category == "Sick Leave" && policy.SickLeaveBonusEligible;
            if (!qualifies)
                return 0m;

            var eligibleUnused = Math.Max(0m, unusedDays);
            if (policy.BonusMaxEligibleUnusedDays.HasValue)
                eligibleUnused = Math.Min(eligibleUnused, policy.BonusMaxEligibleUnusedDays.Value);
            var bonusDays = eligibleUnused * policy.BonusConversionRate;
            if (policy.BonusMaxDaysPerEmployeeYear.HasValue)
                bonusDays = Math.Min(bonusDays, Math.Max(0m, policy.BonusMaxDaysPerEmployeeYear.Value - alreadyAwarded));
            return decimal.Round(bonusDays * 2m, 0, MidpointRounding.AwayFromZero) / 2m;
        }

        private async Task<EmployeeLeaveBalance> GetOrCreateBonusBalanceAsync(
            int employeeId, int leaveYear, string category, CancellationToken cancellationToken)
        {
            var balance = await _db.EmployeeLeaveBalances.FirstOrDefaultAsync(
                item => item.EmployeeId == employeeId && item.LeaveYear == leaveYear && item.LeaveType == category,
                cancellationToken);
            if (balance != null)
                return balance;

            balance = new EmployeeLeaveBalance
            {
                EmployeeId = employeeId,
                LeaveYear = leaveYear,
                LeaveType = category,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };
            _db.EmployeeLeaveBalances.Add(balance);
            return balance;
        }

        private LeaveBalanceTransaction AddTransaction(int employeeId, int leaveYear, string leaveType, string type,
            decimal days, string key, string reason, int? actorUserId)
        {
            var transaction = new LeaveBalanceTransaction
            {
                EmployeeId = employeeId,
                LeaveYear = leaveYear,
                LeaveType = leaveType,
                TransactionType = type,
                Days = days,
                IdempotencyKey = key,
                Reason = reason,
                CreatedByUserId = actorUserId,
                CreatedOn = DateTime.UtcNow
            };
            _db.LeaveBalanceTransactions.Add(transaction);
            return transaction;
        }
    }
}
