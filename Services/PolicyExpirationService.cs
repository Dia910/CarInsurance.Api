using CarInsurance.Api.Data;
using CarInsurance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Services;

public class PolicyExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PolicyExpirationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public PolicyExpirationService(IServiceProvider serviceProvider, ILogger<PolicyExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Policy Expiration Service started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiredPoliciesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking expired policies");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckExpiredPoliciesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var currentDate = DateOnly.FromDateTime(now);
        var oneHourAgo = now.AddHours(-1);

        var expiredPolicies = await context.Policies
            .Include(p => p.Car)
            .Where(p => p.EndDate == currentDate || 
                       (p.EndDate < currentDate && p.EndDate >= DateOnly.FromDateTime(oneHourAgo))) 
            .Where(p => !context.ExpirationLogs.Any(log => log.PolicyId == p.Id && log.ExpiredDate == p.EndDate))
            .ToListAsync();

        foreach (var policy in expiredPolicies)
        {
            var expirationDateTime = policy.EndDate.ToDateTime(TimeOnly.MaxValue); 
            var timeSinceExpiration = now - expirationDateTime;

            if (timeSinceExpiration >= TimeSpan.Zero && timeSinceExpiration <= TimeSpan.FromHours(1))
            {
                var message = $"Insurance policy {policy.Id} for car {policy.Car?.Vin ?? "Unknown"} " +
                             $"(Provider: {policy.Provider}) expired on {policy.EndDate:yyyy-MM-dd}";

                _logger.LogWarning("POLICY EXPIRED: {message}", message);

                var expirationLog = new PolicyExpirationLog
                {
                    PolicyId = policy.Id,
                    ExpiredDate = policy.EndDate,
                    LoggedAt = now,
                    Message = message
                };

                context.ExpirationLogs.Add(expirationLog);
            }
        }

        if (expiredPolicies.Any())
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Processed {count} expired policies at {time}",
                                 expiredPolicies.Count, now);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Policy Expiration Service is stopping at: {time}", DateTimeOffset.Now);
        await base.StopAsync(stoppingToken);
    }
}