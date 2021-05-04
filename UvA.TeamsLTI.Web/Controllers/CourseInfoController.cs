using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var current = await Data.GetCourse(CourseId);
            if (current != null)
                info = await Data.UpdateCourseInfo(info);
            return info;
        }

        async Task<CourseInfo> GetCourseInfo()
        {
            var info = await CourseService.GetCourseInfo(CourseId);
            info.Name = User.FindFirstValue("courseName");
            return info;
        }

        [HttpPost]
        public async Task<string> Post(Team team)
        {
            var current = await Data.GetCourse(CourseId);
            if (current == null)
                await Data.UpdateCourseInfo(await GetCourseInfo());
            await Data.UpdateTeam(CourseId, team);
            return team.Id;
        }

        [HttpPost]
        [Route("Sync")]
        public async Task Sync()
        {
            foreach (var team in (await Data.GetCourse(CourseId)).Teams)
                await Synchronizer.Process(CourseId, team);
        }
    }
}
