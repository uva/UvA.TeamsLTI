using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cv = UvA.DataNose.Connectors.Canvas;
using UvA.TeamsLTI.Data.Models;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Services
{
    public class CanvasService : ICourseService
    {
        Cv.CanvasApiConnector Connector;

        public CanvasService(IConfiguration config)
        {
            Connector = new Cv.CanvasApiConnector(config["Host"], config["Token"]);
        }

        public async Task<CourseInfo> GetCourseInfo(int courseId)
        {
            var crs = new Cv.Course(Connector) { ID = courseId };
            return new CourseInfo
            {
                CourseId = courseId,
                Sections = crs.Sections.Select(s => new Section
                {
                    Name = s.Name,
                    Id = s.ID.Value
                }).ToArray(),
                GroupSets = crs.GroupCategories.Select(c => new GroupSet
                {
                    Name = c.Name,
                    Id = c.ID.Value,
                    GroupCount = c.Groups.Count
                }).ToArray()
            };
        }

        public async Task<IEnumerable<GroupInfo>> GetGroups(int courseId, int groupSetId)
        {
            var gs = new Cv.GroupCategory(Connector) { ID = groupSetId, CourseID = courseId };
            return gs.Groups.Select(g => new GroupInfo
            {
                Name = g.Name,
                Id = g.ID.Value
            });
        }

        public async Task<IEnumerable<UserInfo>> GetUsers(int courseId, Context context)
        {
            var crs = new Cv.Course(Connector) { ID = courseId };
            var users = context.Type switch
            {
                ContextType.Course => crs.GetUsersByType(Cv.EnrollmentType.Student),
                ContextType.Section => new Cv.Section(Connector) { ID = context.Id, CourseID = courseId }.Enrollments.Where(e => e.Type == Cv.EnrollmentType.Student).Select(e => e.User),
                ContextType.Group => new Cv.Group(Connector) { ID = context.Id, GroupCategoryID = context.GroupSetId }.Users,
                _ => throw new NotImplementedException()
            };
            return users.Concat(crs.GetUsersByType(Cv.EnrollmentType.Teacher)).Select(u => new UserInfo
            {
                Username = u.LoginID,
                Email = u.Email,
                Id = u.ID.Value
            });
        }
    }
}
