using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UvA.Connectors.Brightspace;
using Bsp = UvA.Connectors.Brightspace.Models;
using UvA.TeamsLTI.Data.Models;
using System;

namespace UvA.TeamsLTI.Services
{
    public class BrightspaceService : ICourseService
    {
        BrightspaceConnector Connector;

        public BrightspaceService(IConfiguration config)
        {
            var sec = config.GetSection("BrightspaceAPI");
            Connector = new BrightspaceConnector(sec["Host"], sec["AppId"], sec["AppKey"], sec["UserId"], sec["UserKey"]);
        }

        Dictionary<int, Bsp.Course> Courses = new Dictionary<int, Bsp.Course>();
        Bsp.Course GetCourse(int id) => Courses.GetValueOrDefault(id) ?? (Courses[id] = new Bsp.Course { Identifier = id, Connector = Connector });

        public async Task<CourseInfo> GetCourseInfo(int courseId)
        {
            var crs = GetCourse(courseId);
            return new CourseInfo
            {
                CourseId = courseId,
                Sections = (await crs.GetSections()).Select(s => new Section
                {
                    Name = s.Name,
                    Id = s.SectionId
                }).ToArray(),
                GroupSets = (await crs.GetGroupCategories()).Select(c => new GroupSet
                {
                    Name = c.Name,
                    Id = c.GroupCategoryId,
                    GroupCount = c.GroupIds.Length
                }).ToArray()
            };
        }

        public async Task<IEnumerable<GroupInfo>> GetGroups(int courseId, int groupSetId)
            => (await (await GetCourse(courseId).GetGroupCategories()).First(c => c.GroupCategoryId == groupSetId).GetGroups())
                .Select(g => new GroupInfo { Id = g.GroupId, Name = g.Name });

        static readonly int[] RoleIds = new[] { 109, 110 };

        public async Task<IEnumerable<UserInfo>> GetUsers(int courseId, Context context)
        {
            var crs = GetCourse(courseId);
            await Task.WhenAll(RoleIds.Select(r => crs.GetEnrollments(r)));
            var users = crs.Enrollments.Select(e => new UserInfo
                {
                    Username = e.User.OrgDefinedId,
                    Email = e.User.EmailAddress,
                    Id = int.Parse(e.User.Identifier),
                    IsTeacher = e.Role.Id == 109
                }).ToArray();
            switch (context.Type)
            {
                case ContextType.Course:
                    return users;
                case ContextType.Section:
                    var sec = crs.Sections.First(s => s.SectionId == context.Id);
                    return users.Where(u => u.IsTeacher || sec.Enrollments.Contains(u.Id));
                case ContextType.Group:
                    var group = crs.GroupCategories.First(c => c.GroupCategoryId == context.GroupSetId).Groups.First(g => g.GroupId == context.Id);
                    return users.Where(u => u.IsTeacher || group.Enrollments.Contains(u.Id));
            }
            throw new ArgumentException("Invalid ContextType");
        }
    }
}
