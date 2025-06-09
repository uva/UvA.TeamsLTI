using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Data.Models;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Web.Services;

/// <summary>
/// Removes Teams for which the course no longer exists
/// </summary>
public class CleanupService(TeamSynchronizerResolver resolver, TeamsData data, ILogger<CleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = DateTime.Now.Date.AddHours(3);
            if (next < DateTime.Now)
                next = DateTime.Now.Date.AddDays(1);
            await Task.Delay(next.Subtract(DateTime.Now), stoppingToken);
            if (DateTime.Now.DayOfWeek != DayOfWeek.Sunday)
                continue;
            await Cleanup();
        }
    }

    private async Task Cleanup()
    {
        logger.LogInformation("Cleaning up Teams");
        foreach (var env in resolver.GetEnvironments())
        {
            logger.LogInformation(env);
            var client = resolver.GetCourseService(env);
            var sync = resolver.Get(env);
            var courses = await data.GetCourses(env);
            foreach (var course in courses)
            {
                if (await client.CourseExists(course.CourseId))
                    continue;

                foreach (var team in course.Teams.Where(t => t.GroupId != null))
                {
                    team.DeleteEvent = new Event {Date = DateTime.Now, User = "CleanupService"};
                    logger.LogInformation($"Deleting team {team.Name} ({team.GroupId}) for course {course.Name} ({course.CourseId})");
                    // TODO: enable this
                    //await sync.Process(env, course.CourseId, team, true);
                }
            }
        }
        logger.LogInformation("All done");
    }
}