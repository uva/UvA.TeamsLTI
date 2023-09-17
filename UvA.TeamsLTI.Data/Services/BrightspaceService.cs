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
        private readonly BrightspaceConnector _connector;
        private readonly string _host;

        private readonly int[] _roleIds;
        private readonly int[] _coordinatorIds;
        private readonly int _studentId;
        
        public BrightspaceService(IConfiguration config)
        {
            _connector = new BrightspaceConnector(_host = config["Host"], config["AppId"], config["AppKey"], config["UserId"], config["UserKey"]);
            _roleIds = config["RoleIds"]?.Split(',').Select(int.Parse).ToArray() ?? new[]
            {
                109, // designing lecturer
                110, // student
                125  // lecturer
            };
            _studentId = int.TryParse(config["StudentId"], out var x) ? x : 110;
            _coordinatorIds = config["CoordinatorIds"]?.Split(',').Select(int.Parse).ToArray() ?? new[] {109};
        }

        private readonly Dictionary<int, Bsp.Course> _courses = new();
        private Bsp.Course GetCourse(int id) => _courses.GetValueOrDefault(id) ?? (_courses[id] = new Bsp.Course { Identifier = id, Connector = _connector });

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
                CourseUrl = $"{_host}/d2l/home/{courseId}"
            };
        }

        public async Task<IEnumerable<GroupInfo>> GetGroups(int courseId, int groupSetId)
            => (await (await GetCourse(courseId).GetGroupCategories()).First(c => c.GroupCategoryId == groupSetId).GetGroups())
                .Select(g => new GroupInfo { Id = g.GroupId, Name = g.Name });
        
        public async Task<IEnumerable<UserInfo>> GetUsers(int courseId, Context context)
        {
            var crs = GetCourse(courseId);
            await Task.WhenAll(_roleIds.Select(r => crs.GetEnrollments(r)));
            var users = crs.Enrollments.Select(e => new UserInfo
                {
                    Username = e.User.OrgDefinedId,
                    Email = e.User.EmailAddress,
                    Id = int.Parse(e.User.Identifier),
                    IsTeacher = e.Role.Id != _studentId,
                    IsCoordinator = _coordinatorIds.Contains(e.Role.Id)
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
