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

        const string EditRoles = LoginController.Manager + "," + LoginController.Teacher;

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
            info.Teams = info.Teams.Where(t => t.DeleteEvent == null).OrderBy(t => t.Name).ToArray();
            if (!User.IsInRole(LoginController.Teacher) && !User.IsInRole(LoginController.Manager))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                info.Teams = info.Teams.Where(t => t.Users.ContainsKey(userId)).ToArray();
            }
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
        [Authorize(Roles = EditRoles)]
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
        [Authorize(Roles = EditRoles)]
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
        [Authorize(Roles = EditRoles)]
        public async Task Sync()
        {
            foreach (var team in (await Data.GetCourse(Environment, CourseId)).Teams.Where(t => t.DeleteEvent?.DateExecuted == null).ToArray())
                await Synchronizer.Process(Environment, CourseId, team);
        }

        [HttpPost]
        [Route("BecomeOwner/{teamId}")]
        [Authorize(Roles = EditRoles)]
        public async Task BecomeOwner(string teamId)
        {
            var current = await Data.GetCourse(Environment, CourseId);
            var team = current.Teams.First(t => t.Id == teamId);
            await Synchronizer.AddOwner(Environment, team, User.FindFirstValue(ClaimTypes.Email));
        }
    }
}
