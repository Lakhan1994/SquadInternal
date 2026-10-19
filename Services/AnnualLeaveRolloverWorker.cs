using Microsoft.EntityFrameworkCore;
using SquadInternal.Data;

namespace SquadInternal.Services
{
    public class AnnualLeaveRolloverWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AnnualLeaveRolloverWorker> _logger;

        public AnnualLeaveRolloverWorker(IServiceScopeFactory scopeFactory, ILogger<AnnualLeaveRolloverWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessDueRolloverAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private async Task ProcessDueRolloverAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var rollover = scope.ServiceProvider.GetRequiredService<AnnualLeaveRolloverService>();
                await rollover.ExpireDueBonusesAsync(cancellationToken);
                var policy = await db.AnnualLeavePolicies
                    .OrderByDescending(item => item.EffectiveFrom)
                    .FirstOrDefaultAsync(cancellationToken);
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(policy?.BusinessTimeZoneId ?? "UTC");
                var businessDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
                if (businessDate.Year < 2027)
                    return;

                await rollover.ProcessYearAsync(businessDate.Year, null, cancellationToken);
                var leavePolicyService = scope.ServiceProvider.GetRequiredService<AnnualLeavePolicyService>();
                await leavePolicyService.ProcessMonthlyAccrualsAsync(businessDate, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Annual leave rollover processing failed.");
            }
        }
    }
}
