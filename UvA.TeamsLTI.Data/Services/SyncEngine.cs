using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data;

namespace UvA.TeamsLTI.Services
{
    public class SyncEngine
    {
        TeamSynchronizerResolver Resolver;
        TeamsData Data;
        IConfiguration Config;
        ILogger Logger;

        public SyncEngine(IConfiguration config, TeamsData data, TeamSynchronizerResolver resolver, ILogger<SyncEngine> logger)
        {
            Data = data;
            Resolver = resolver;
            Config = config;
            Logger = logger;
        }

        public async Task SyncAll()
        {
            Logger.LogInformation("Running full sync");
            foreach (var env in Config.GetSection("Environments").GetChildren().Select(e => e["Host"]).ToArray())
            {
                var sync = Resolver.Get(env);
                foreach (var course in await Data.GetRelevantCourses(env))
                {
                    foreach (var team in course.Teams.Where(t => t.GroupId != null))
                    {
                        try
                        {
                            await sync.Process(course.Environment, course.CourseId, team, true);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Failed to sync team {team.Name} ({team.GroupId}) in {course.Environment}:{course.CourseId}");
                        }
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
            var config = Config.GetSection("Environments").GetChildren().First(c => c["Host"] == env);
            ICourseService courseService = env.Contains("canvas") ? new CanvasService(config) : new BrightspaceService(config);
            return new TeamSynchronizer(Config, Data, courseService, Logger);
        }
    }
}
