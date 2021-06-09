using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Data.Models;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Web.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CourseInfoController : ControllerBase
    {
        ICourseService CourseService;
        TeamsData Data;
        TeamSynchronizer Synchronizer;

        string Environment => User.FindFirstValue("environment");

        public CourseInfoController(ICourseService cs, TeamsData data, TeamSynchronizer sync)
        {
            CourseService = cs;
            Data = data;
            Synchronizer = sync;
        }

        int CourseId => int.Parse(User.FindFirstValue("courseId"));

        public async Task<CourseInfo> Get()
        {
            var info = await GetCourseInfo();
            var current = await Data.GetCourse(Environment, CourseId);
            if (current != null)
                info = await Data.UpdateCourseInfo(info);
            info.Teams = info.Teams.Where(t => t.DeleteEvent == null).ToArray();
            return info;
        }

        async Task<CourseInfo> GetCourseInfo()
        {
            var info = await CourseService.GetCourseInfo(CourseId);
            info.Name = User.FindFirstValue("courseName");
            info.Environment = Environment;
            return info;
        }

        Event GetEvent() => new Event
        {
            Date = DateTime.Now,
            User = User.FindFirstValue(ClaimTypes.Email)
        };

        [HttpPost]
        [Authorize(Roles = LoginController.Teacher)]
        public async Task<string> Post(Team team)
        {
            var current = await Data.GetCourse(Environment, CourseId);
            if (current == null)
                await Data.UpdateCourseInfo(await GetCourseInfo());
            if (team.CreateEvent == null)
                team.CreateEvent = GetEvent();
            await Data.UpdateTeam(Environment, CourseId, team);
            return team.Id;
        }

        [HttpDelete]
        [Route("{teamId}")]
        [Authorize(Roles = LoginController.Teacher)]
        public async Task Delete(string teamId)
        {
            var current = await Data.GetCourse(Environment, CourseId);
            var team = current.Teams.First(t => t.Id == teamId);
            team.DeleteEvent = GetEvent();
            await Synchronizer.Process(Environment, CourseId, team);
            await Data.UpdateTeam(Environment, CourseId, team);
        }

        [HttpPost]
        [Route("Sync")]
        [Authorize(Roles = LoginController.Teacher)]
        public async Task Sync()
        {
            foreach (var team in (await Data.GetCourse(Environment, CourseId)).Teams)
                await Synchronizer.Process(Environment, CourseId, team);
        }
    }
}
