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
        string Host;

        public BrightspaceService(IConfiguration config)
        {
            Connector = new BrightspaceConnector(Host = config["Host"], config["AppId"], config["AppKey"], config["UserId"], config["UserKey"]);
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
                }).ToArray(),
                CourseUrl = $"{Host}/d2l/home/{courseId}"
            };
        }

        public async Task<IEnumerable<GroupInfo>> GetGroups(int courseId, int groupSetId)
            => (await (await GetCourse(courseId).GetGroupCategories()).First(c => c.GroupCategoryId == groupSetId).GetGroups())
                .Select(g => new GroupInfo { Id = g.GroupId, Name = g.Name });

        static readonly int[] RoleIds = new[] 
        {
            109, // designing lecturer 
            110, // student
            124, // lecturer plus
            125, // lecturer
            126, // supporting lecturer
        };

        public async Task<IEnumerable<UserInfo>> GetUsers(int courseId, Context context)
        {
            var crs = GetCourse(courseId);
            await Task.WhenAll(RoleIds.Select(r => crs.GetEnrollments(r)));
            var users = crs.Enrollments.Select(e => new UserInfo
                {
                    Username = e.User.OrgDefinedId,
                    Email = e.User.EmailAddress,
                    Id = int.Parse(e.User.Identifier),
                    IsTeacher = e.Role.Id != 110,
                    IsCoordinator = e.Role.Id == 109
                }).ToArray();
            switch (context.Type)
            {
                case ContextType.Course:
                    return users;
                case ContextType.Section:
                    var sec = crs.Sections.FirstOrDefault(s => s.SectionId == context.Id);
                    return users.Where(u => u.IsCoordinator || sec?.Enrollments.Contains(u.Id) == true);
                case ContextType.Group:
                    var group = crs.GroupCategories.FirstOrDefault(c => c.GroupCategoryId == context.GroupSetId)?.Groups.FirstOrDefault(g => g.GroupId == context.Id);
                    return users.Where(u => u.IsCoordinator || group?.Enrollments.Contains(u.Id) == true);
            }
            throw new ArgumentException("Invalid ContextType");
        }
    }
}
