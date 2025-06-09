using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Web.Services
{
    public class SyncService(SyncEngine engine, ILogger<SyncService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
#if DEBUG
            return;
#endif
            while (!stoppingToken.IsCancellationRequested)
            {
                var next = DateTime.Now.Date.AddHours(1);
                if (next < DateTime.Now)
                    next = DateTime.Now.Date.AddDays(1);
                logger.LogInformation($"Running sync in {next.Subtract(DateTime.Now)}");
                await Task.Delay(next.Subtract(DateTime.Now), stoppingToken);
                await engine.SyncAll();
            }
        }
    }
}
