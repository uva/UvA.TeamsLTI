using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UvA.Connectors.Canvas.Helpers;
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
    
    private record LogEntry(bool Success, int CourseId, string CourseName, string TeamName, string TeamId, string Error);

    private async Task Cleanup()
    {
        var reportClient = new HttpClient();
        
        logger.LogInformation("Cleaning up Teams");
        foreach (var env in resolver.GetEnvironments())
        {
            logger.LogInformation(env);
            var client = resolver.GetCourseService(env);
            var sync = resolver.Get(env);
            var courses = await data.GetCourses(env);
            var report = new List<LogEntry>();
            foreach (var course in courses.Where(c => c.Teams.Any(t => t.GroupId != null && t.DeleteEvent?.DateExecuted == null)))
            {
                try
                {
                    if (await client.CourseExists(course.CourseId))
                        continue;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error retrieving {courseId}", course.CourseId);
                    report.Add(new LogEntry(false, course.CourseId, course.Name, null, null, ex.Message));
                    continue;
                }

                foreach (var team in course.Teams.Where(t => t.GroupId != null && t.DeleteEvent?.DateExecuted == null))
                {
                    team.DeleteEvent = new Event {Date = DateTime.Now, User = "CleanupService"};
                    logger.LogInformation($"Deleting team {team.Name} ({team.GroupId}) for course {course.Name} ({course.CourseId})");
                    await sync.Process(env, course.CourseId, team, true);
                    report.Add(new LogEntry(true, course.CourseId, course.Name, team.Name, team.GroupId, null));
                }
            }

            var reportUrl = resolver.GetReportUrl(env);
            if (!string.IsNullOrEmpty(reportUrl))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, reportUrl);
                var content = JsonContent.Create(new
                {
                    environment = env,
                    body = $"""
                            {report.Count(r => r.Success)} succeeded, {report.Count(r => !r.Success)} failed:

                            Deleted teams:
                            {report.Where(r => r.Success).ToSeparatedString(r => $"- {r.TeamName} ({r.TeamId}) for course {r.CourseName} ({r.CourseId})", "\n")}

                            Errors:
                            {report.Where(r => !r.Success).ToSeparatedString(r => $"- {r.CourseName} ({r.CourseId}): {r.Error}", "\n")}
                            """.Replace("\n", "\n<br /")
                });
                await content.LoadIntoBufferAsync(); // need this because Logic Apps can't deal with chunking
                request.Content = content;
                await reportClient.SendAsync(request);
            }
        }
        
        logger.LogInformation("All done");
    }
}