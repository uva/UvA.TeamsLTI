using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data;

namespace UvA.TeamsLTI.Services
{
    public class SyncEngine(TeamsData data, TeamSynchronizerResolver resolver, ILogger<SyncEngine> logger)
    {
        public async Task SyncAll()
        {
            logger.LogInformation("Running full sync");
            foreach (var env in resolver.GetEnvironments())
            {
                var sync = resolver.Get(env);
                foreach (var course in await data.GetRelevantCourses(env))
                {
                    foreach (var team in course.Teams.Where(t => t.GroupId != null))
                    {
                        try
                        {
                            await sync.Process(course.Environment, course.CourseId, team, true);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"Failed to sync team {team.Name} ({team.GroupId}) in {course.Environment}:{course.CourseId}");
                        }
                    }
                }
            }
        }
    }

    public class TeamSynchronizerResolver(IConfiguration config, TeamsData data, ILogger<TeamSynchronizer> log)
    {
        public string[] GetEnvironments()
            => config.GetSection("Environments").GetChildren().Select(e => e["Host"]).ToArray();
        
        public ICourseService GetCourseService(string env)
        {
            var section = config.GetSection("Environments").GetChildren().First(c => c["Host"] == env);
            return env.Contains("canvas") || env.Contains("instructure") ? new CanvasService(section) : new BrightspaceService(section);
        }
        
        public TeamSynchronizer Get(string env) => new(config, data, GetCourseService(env), log);
    }
}
