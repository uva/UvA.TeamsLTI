using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data.Models;

namespace UvA.TeamsLTI.Data
{
    public class TeamsData
    {
        IMongoDatabase Database;
        IMongoCollection<CourseInfo> Courses;

        public TeamsData()
        {
            Database = new MongoClient().GetDatabase("teams");
            Courses = Database.GetCollection<CourseInfo>("Courses");
        }

        public async Task<CourseInfo> GetCourse(int id)
            => await (await Courses.FindAsync(f => f.Id == id)).FirstOrDefaultAsync();

        public async Task<CourseInfo> UpdateCourseInfo(CourseInfo info)
        {
            var existing = await GetCourse(info.Id);
            if (existing == null)
                await Courses.InsertOneAsync(info);
            else
            {
                info.Teams = existing.Teams;
                await Courses.ReplaceOneAsync(i => i.Id == info.Id, info);
                return info;
            }
            return existing;
        }

        public async Task UpdateTeam(int id, Team team)
        {
            var course = await GetCourse(id);
            var existing = course.Teams.FirstOrDefault(t => t.Id == team.Id);
            if (existing == null)
                await Courses.UpdateOneAsync(i => i.Id == id, Builders<CourseInfo>.Update.Push(i => i.Teams, team));
            else
            {
                existing.Contexts = team.Contexts;
                existing.AllowChannels = team.AllowChannels;
                existing.AllowPrivateChannels = team.AllowPrivateChannels;
                existing.Name = team.Name;
                existing.GroupSetIds = team.GroupSetIds;
                existing.CreateSectionChannels = team.CreateSectionChannels;
                if (team.GroupId != null)
                {
                    existing.GroupId = team.GroupId;
                    existing.Url = team.Url;
                }

                await Courses.UpdateOneAsync(TeamFilter(id, existing.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1], existing));
            }
        }

        FilterDefinitionBuilder<CourseInfo> Filter => Builders<CourseInfo>.Filter;
        FilterDefinition<CourseInfo> TeamFilter(int id, string teamId) => Filter.Eq(c => c.Id, id) & Filter.ElemMatch(c => c.Teams, t => t.Id == teamId);

        public Task UpdateChannels(int id, Team team)
            => Courses.UpdateOneAsync(TeamFilter(id, team.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1].Channels, team.Channels));

        public Task UpdateUsers(int id, Team team)
            => Courses.UpdateOneAsync(TeamFilter(id, team.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1].Users, team.Users));
    }
}
