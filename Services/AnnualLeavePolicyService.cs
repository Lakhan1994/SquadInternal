using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;
using SquadInternal.Models;

namespace SquadInternal.Services
{
    public class AnnualLeavePolicyService
    {
        private readonly AppDbContext _db;

        public AnnualLeavePolicyService(AppDbContext db)
        {
            _db = db;
        }

        public Task<AnnualLeavePolicy?> GetPolicyAsync(int leaveYear, CancellationToken cancellationToken = default) =>
            _db.AnnualLeavePolicies
                .Where(policy => policy.EffectiveFrom.Year <= leaveYear)
                .OrderByDescending(policy => policy.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

        public static DateTime? GetFirstEligibleAccrualMonth(DateTime? joiningDate)
        {
            if (!joiningDate.HasValue)
                return null;

            var joined = joiningDate.Value.Date;
            var firstOfJoinMonth = new DateTime(joined.Year, joined.Month, 1);
            return joined.Day == 1 ? firstOfJoinMonth : firstOfJoinMonth.AddMonths(1);
        }

        public static int GetEligibleAccrualMonths(DateTime? joiningDate, int leaveYear, int throughMonth = 12)
        {
            if (throughMonth is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(throughMonth));

            var firstEligibleMonth = GetFirstEligibleAccrualMonth(joiningDate);
            if (!firstEligibleMonth.HasValue)
                return 0;

            var firstMonth = firstEligibleMonth.Value > new DateTime(leaveYear, 1, 1)
                ? firstEligibleMonth.Value
                : new DateTime(leaveYear, 1, 1);
            var lastMonth = new DateTime(leaveYear, throughMonth, 1);
            if (firstMonth > lastMonth)
                return 0;

            return (lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1;
        }

        public static decimal CalculateAllocation(decimal monthlyAccrualDays, DateTime? joiningDate, int leaveYear)
        {
            if (monthlyAccrualDays <= 0m)
                return 0m;

            return monthlyAccrualDays * GetEligibleAccrualMonths(joiningDate, leaveYear);
        }

        public async Task<EmployeeLeaveBalance?> GetOrCreateBalanceAsync(
            Employee employee,
            int leaveYear,
            string leaveType,
            CancellationToken cancellationToken = default)
        {
            var policy = await GetPolicyAsync(leaveYear, cancellationToken);
            if (policy == null)
                return null;

            var normalizedType = NormalizeAnnualLeaveType(leaveType);
            if (normalizedType == null ||
                (normalizedType == "Earned Leave" && !policy.EarnedLeaveEnabled) ||
                (normalizedType == "Bonus Leave" && !policy.BonusUsesSeparateCategory))
                return null;

            var balance = await _db.EmployeeLeaveBalances.FirstOrDefaultAsync(
                item => item.EmployeeId == employee.Id && item.LeaveYear == leaveYear && item.LeaveType == normalizedType,
                cancellationToken);
            if (balance != null)
                return balance;

            var monthlyAccrualDays = GetMonthlyAccrualDays(normalizedType);
            var allocation = CalculateAllocation(monthlyAccrualDays, employee.DateOfJoining, leaveYear);
            var firstEligibleMonth = GetFirstEligibleAccrualMonth(employee.DateOfJoining);
            var yearStart = new DateTime(leaveYear, 1, 1);
            var isProrated = employee.DateOfJoining?.Year == leaveYear &&
                firstEligibleMonth.HasValue && firstEligibleMonth.Value > yearStart;

            balance = new EmployeeLeaveBalance
            {
                EmployeeId = employee.Id,
                LeaveYear = leaveYear,
                LeaveType = normalizedType,
                AnnualEntitlement = isProrated ? 0m : allocation,
                ProratedEntitlement = isProrated ? allocation : 0m,
                ClosingBalance = 0m,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };

            _db.EmployeeLeaveBalances.Add(balance);
            await _db.SaveChangesAsync(cancellationToken);
            return balance;
        }

        public static decimal GetMonthlyAccrualDays(string leaveType) => NormalizeAnnualLeaveType(leaveType) switch
        {
            "Earned Leave" => 0.5m,
            "Sick Leave" or "Casual Leave" => 0.25m,
            _ => 0m
        };

        public async Task AccrueThroughMonthAsync(
            Employee employee,
            int leaveYear,
            string leaveType,
            int throughMonth,
            CancellationToken cancellationToken = default)
        {
            if (throughMonth is < 1 or > 12)
                throw new ArgumentOutOfRangeException(nameof(throughMonth));

            var policy = await GetPolicyAsync(leaveYear, cancellationToken);
            var category = NormalizeAnnualLeaveType(leaveType);
            if (policy == null || category == null || employee.DateOfJoining == null ||
                (category == "Earned Leave" && !policy.EarnedLeaveEnabled))
                return;

            var monthlyDays = GetMonthlyAccrualDays(category);
            if (monthlyDays <= 0m)
                return;
            var firstEligibleMonth = GetFirstEligibleAccrualMonth(employee.DateOfJoining);
            var yearStart = new DateTime(leaveYear, 1, 1);
            var lastMonth = new DateTime(leaveYear, throughMonth, 1);
            var month = firstEligibleMonth!.Value > yearStart ? firstEligibleMonth.Value : yearStart;
            if (month > lastMonth)
                return;

            var balance = await GetOrCreateBalanceAsync(employee, leaveYear, category, cancellationToken);
            if (balance == null)
                return;

            for (; month <= lastMonth; month = month.AddMonths(1))
            {
                var key = $"monthly-accrual:{employee.Id}:{leaveYear}:{category}:{month:yyyyMM}";
                if (await _db.LeaveBalanceTransactions.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
                    continue;

                balance.ClosingBalance += monthlyDays;
                balance.UpdatedOn = DateTime.UtcNow;
                AddTransaction(employee.Id, leaveYear, category, "MonthlyAccrual", monthlyDays, key,
                    $"Monthly accrual for {month:yyyy-MM}");
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ProcessMonthlyAccrualsAsync(DateTime asOfDate, CancellationToken cancellationToken = default)
        {
            if (asOfDate.Year < 2027)
                return;

            var employees = await _db.Employees
                .Where(employee => employee.IsActive && employee.DateOfJoining.HasValue &&
                    employee.DateOfJoining.Value.Date <= asOfDate.Date)
                .ToListAsync(cancellationToken);

            foreach (var employee in employees)
            {
                for (var year = 2027; year <= asOfDate.Year; year++)
                {
                    var policy = await GetPolicyAsync(year, cancellationToken);
                    if (policy == null)
                        continue;

                    await AccrueThroughMonthAsync(employee, year, "Casual Leave",
                        year == asOfDate.Year ? asOfDate.Month : 12, cancellationToken);
                    await AccrueThroughMonthAsync(employee, year, "Sick Leave",
                        year == asOfDate.Year ? asOfDate.Month : 12, cancellationToken);
                    if (policy.EarnedLeaveEnabled)
                        await AccrueThroughMonthAsync(employee, year, "Earned Leave",
                            year == asOfDate.Year ? asOfDate.Month : 12, cancellationToken);
                }
            }
        }

        public async Task<decimal> GetAvailableBalanceAsync(
            EmployeeLeaveBalance balance,
            CancellationToken cancellationToken = default)
        {
            var businessDate = await GetBusinessDateAsync(balance.LeaveYear, cancellationToken);
            if (businessDate.Year == balance.LeaveYear)
            {
                var employee = await _db.Employees.FirstOrDefaultAsync(item => item.Id == balance.EmployeeId, cancellationToken);
                if (employee != null)
                    await AccrueThroughMonthAsync(employee, balance.LeaveYear, balance.LeaveType,
                        businessDate.Month, cancellationToken);
            }

            await ExpireBonusCreditsAsync(balance, cancellationToken);
            var yearStart = new DateTime(balance.LeaveYear, 1, 1);
            var yearEnd = new DateTime(balance.LeaveYear, 12, 31);
            var yearHolidays = await GetHolidayMapAsync(yearStart, yearEnd, cancellationToken);
            var pending = await _db.EmployeeLeaves
                .Where(leave => leave.EmployeeId == balance.EmployeeId &&
                    leave.Status == "Pending" &&
                    leave.FromDate.Year <= balance.LeaveYear && leave.ToDate.Year >= balance.LeaveYear)
                .ToListAsync(cancellationToken);

            var reserved = pending
                .Where(leave => NormalizeAnnualLeaveType(leave.LeaveType) == balance.LeaveType)
                .Sum(leave => LeaveDayCalculator.Calculate(
                MaxDate(leave.FromDate.Date, yearStart),
                MinDate(leave.ToDate.Date, yearEnd),
                leave.Duration,
                yearHolidays));

            return Math.Max(0m, balance.ClosingBalance - reserved);
        }

        public async Task ExpireBonusCreditsAsync(EmployeeLeaveBalance balance, CancellationToken cancellationToken = default)
        {
            var dueCredits = await _db.LeaveBalanceTransactions
                .Where(item => item.EmployeeId == balance.EmployeeId && item.LeaveYear == balance.LeaveYear &&
                    item.LeaveType == balance.LeaveType && item.TransactionType == "BonusCredit" &&
                    item.ExpiresOn.HasValue && item.ExpiresOn.Value <= DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var credit in dueCredits)
            {
                var key = $"bonus-expiration:{credit.Id}";
                if (await _db.LeaveBalanceTransactions.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
                    continue;

                var expiredDays = Math.Min(credit.Days, Math.Max(0m, balance.ClosingBalance));
                if (expiredDays <= 0m)
                    continue;

                balance.ExpiredBalance += expiredDays;
                balance.ClosingBalance -= expiredDays;
                balance.UpdatedOn = DateTime.UtcNow;
                _db.LeaveBalanceTransactions.Add(new LeaveBalanceTransaction
                {
                    EmployeeId = balance.EmployeeId,
                    LeaveYear = balance.LeaveYear,
                    LeaveType = balance.LeaveType,
                    TransactionType = "BonusExpiration",
                    Days = -expiredDays,
                    CreatedOn = DateTime.UtcNow,
                    IdempotencyKey = key,
                    Reason = $"Bonus credit {credit.Id} expired"
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> RecordApprovalAsync(EmployeeLeave leave, int? approverUserId, CancellationToken cancellationToken = default)
        {
            var leaveType = NormalizeAnnualLeaveType(leave.LeaveType);
            if (leaveType == null || leave.FromDate.Year != leave.ToDate.Year)
                return true;

            var balance = await GetOrCreateBalanceAsync(leave.Employee!, leave.FromDate.Year, leaveType, cancellationToken);
            if (balance == null)
                return true;

            var businessDate = await GetBusinessDateAsync(leave.FromDate.Year, cancellationToken);
            if (leave.FromDate.Year == businessDate.Year)
                await AccrueThroughMonthAsync(leave.Employee!, leave.FromDate.Year, leaveType,
                    businessDate.Month, cancellationToken);

            var key = $"leave-approval:{leave.Id}";
            if (await _db.LeaveBalanceTransactions.AnyAsync(item => item.IdempotencyKey == key, cancellationToken))
                return true;

            var yearStart = new DateTime(leave.FromDate.Year, 1, 1);
            var yearEnd = new DateTime(leave.FromDate.Year, 12, 31);
            var holidays = await GetHolidayMapAsync(yearStart, yearEnd, cancellationToken);
            var days = LeaveDayCalculator.Calculate(leave.FromDate, leave.ToDate, leave.Duration, holidays);
            var otherPending = await _db.EmployeeLeaves
                .Where(item => item.EmployeeId == leave.EmployeeId && item.Id != leave.Id &&
                    item.Status == "Pending" &&
                    item.FromDate.Year <= leave.FromDate.Year && item.ToDate.Year >= leave.FromDate.Year)
                .ToListAsync(cancellationToken);
            var reservedDays = otherPending
                .Where(item => NormalizeAnnualLeaveType(item.LeaveType) == leaveType)
                .Sum(item => LeaveDayCalculator.Calculate(
                MaxDate(item.FromDate.Date, yearStart),
                MinDate(item.ToDate.Date, yearEnd),
                item.Duration,
                holidays));
            if (balance.ClosingBalance - reservedDays < days)
                return false;

            balance.ApprovedConsumption += days;
            balance.ClosingBalance -= days;
            balance.UpdatedOn = DateTime.UtcNow;
            AddTransaction(leave.EmployeeId, leave.FromDate.Year, leaveType, "ApprovedConsumption", -days, key,
                $"Approved leave request {leave.Id}", approverUserId, leave.Id);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public static string? NormalizeAnnualLeaveType(string? leaveType) => leaveType?.Trim().ToLowerInvariant() switch
        {
            "casual" or "casual leave" or "cl" => "Casual Leave",
            "sick" or "sick leave" or "sl" => "Sick Leave",
            "earned" or "earned leave" or "privilege leave" or "el" or "pl" or "el/pl" => "Earned Leave",
            "bonus leave" => "Bonus Leave",
            _ => null
        };

        public async Task<IReadOnlyDictionary<DateTime, bool>> GetHolidayMapAsync(
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            var rows = await _db.SquadHolidays
                .Where(holiday => holiday.IsActive && holiday.HolidayDate.Date >= fromDate.Date && holiday.HolidayDate.Date <= toDate.Date)
                .Select(holiday => new { holiday.HolidayDate, holiday.IsHalfDay })
                .ToListAsync(cancellationToken);
            return rows.GroupBy(row => row.HolidayDate.Date)
                .ToDictionary(group => group.Key, group => group.All(row => row.IsHalfDay));
        }

        private void AddTransaction(int employeeId, int leaveYear, string leaveType, string transactionType,
            decimal days, string idempotencyKey, string reason, int? actorUserId = null, int? leaveId = null)
        {
            _db.LeaveBalanceTransactions.Add(new LeaveBalanceTransaction
            {
                EmployeeId = employeeId,
                LeaveYear = leaveYear,
                LeaveType = leaveType,
                TransactionType = transactionType,
                Days = days,
                IdempotencyKey = idempotencyKey,
                Reason = reason,
                CreatedByUserId = actorUserId,
                EmployeeLeaveId = leaveId,
                CreatedOn = DateTime.UtcNow
            });
        }

        private async Task<DateTime> GetBusinessDateAsync(int leaveYear, CancellationToken cancellationToken)
        {
            var policy = await GetPolicyAsync(leaveYear, cancellationToken);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(policy?.BusinessTimeZoneId ?? "UTC");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        }

        private static DateTime MaxDate(DateTime left, DateTime right) => left > right ? left : right;
        private static DateTime MinDate(DateTime left, DateTime right) => left < right ? left : right;
    }
}