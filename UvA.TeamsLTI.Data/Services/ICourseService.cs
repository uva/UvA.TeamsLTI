using System.Collections.Generic;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data.Models;

namespace UvA.TeamsLTI.Services
{
    public interface ICourseService
    {
        Task<bool> CourseExists(int courseId);
        Task<CourseInfo> GetCourseInfo(int courseId);
        Task<IEnumerable<GroupInfo>> GetGroups(int courseId, int groupSetId);
        Task<IEnumerable<UserInfo>> GetUsers(int courseId, Context context);
    }
}
