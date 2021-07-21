﻿using Microsoft.Extensions.Configuration;
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
        TeamSynchronizerResolver Resolver;
        TeamsData Data;
        IConfiguration Config;

        public SyncService(IConfiguration config, TeamsData data, TeamSynchronizerResolver resolver)
        {
            Data = data;
            Resolver = resolver;
            Config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var next = DateTime.Now.Date.AddHours(25);
                await Task.Delay(next.Subtract(DateTime.Now), stoppingToken);

                foreach (var env in Config.GetSection("Environments").GetChildren().Select(e => e["Authority"]).ToArray())
                {
                    var sync = Resolver.Get(env);
                    foreach (var course in await Data.GetRelevantCourses(env))
                    {
                        foreach (var team in course.Teams.Where(t => t.GroupId != null))
                            await sync.Process(course.Environment, course.CourseId, team, true);
                    }
                }
            }
        }
    }

    public class TeamSynchronizerResolver
    {
        ILogger<TeamSynchronizer> Logger;
        TeamsData Data;
        IConfiguration Config;

        public TeamSynchronizerResolver(IConfiguration config, TeamsData data, ILogger<TeamSynchronizer> log)
        {
            Config = config;
            Data = data;
            Logger = log;
        }

        public TeamSynchronizer Get(string env)
        {
            var config = Config.GetSection("Environments").GetChildren().First(c => c["Authority"] == env);
            ICourseService courseService = env.Contains("canvas") ? new CanvasService(config) : new BrightspaceService(config);
            return new TeamSynchronizer(Config, Data, courseService, Logger);
        }
    }
}