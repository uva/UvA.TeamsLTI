using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Web.Services
{
    public class SyncService : BackgroundService
    {
        SyncEngine Engine;
        ILogger Logger;

        public SyncService(SyncEngine engine, ILogger<SyncService> logger)
        {
            Engine = engine;
            Logger = logger;
        }

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
                Logger.LogInformation($"Running sync in {next.Subtract(DateTime.Now)}");
                await Task.Delay(next.Subtract(DateTime.Now), stoppingToken);
                await Engine.SyncAll();
            }
        }
    }
}
