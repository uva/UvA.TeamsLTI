﻿using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using UvA.TeamsLTI.Data.Models;

namespace UvA.TeamsLTI.Data
{
    public class TeamsData
    {
        IMongoDatabase Database;
        public IMongoCollection<CourseInfo> Courses;

        public TeamsData(IConfiguration config)
        {
            MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(config["ConnectionString"]));
            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            Database = new MongoClient(settings).GetDatabase("teams");
            Courses = Database.GetCollection<CourseInfo>("Courses");
        }

        public async Task<IEnumerable<CourseInfo>> GetRelevantCourses(string env)
            => await (await Courses.FindAsync(t => t.Environment == env && (t.EndDate == null || t.EndDate > DateTime.Now))).ToListAsync();

        public async Task<CourseInfo> GetCourse(string env, int id)
            => await (await Courses.FindAsync(f => f.Environment == env && f.CourseId == id)).FirstOrDefaultAsync();

        public async Task<CourseInfo> UpdateCourseInfo(CourseInfo info)
        {
            var existing = await GetCourse(info.Environment, info.CourseId);
            if (existing == null)
                await Courses.InsertOneAsync(info);
            else
            {
                info.Teams = existing.Teams;
                info.Id = existing.Id;
                await Courses.ReplaceOneAsync(i => i.CourseId == info.CourseId && i.Environment == info.Environment, info);
                return info;
            }
            return existing;
        }

        public async Task UpdateTeam(string env, int id, Team team)
        {
            var course = await GetCourse(env, id);
            var existing = course.Teams.FirstOrDefault(t => t.Id == team.Id);
            if (existing == null)
                await Courses.UpdateOneAsync(i => i.CourseId == id && i.Environment == env,
                    Builders<CourseInfo>.Update.Push(i => i.Teams, team));
            else
            {
                existing.Contexts = team.Contexts;
                existing.AllowChannels = team.AllowChannels;
                existing.AllowPrivateChannels = team.AllowPrivateChannels;
                existing.AddAllLecturers = team.AddAllLecturers;
                existing.Name = team.Name;
                existing.GroupSetIds = team.GroupSetIds;
                existing.CreateSectionChannels = team.CreateSectionChannels;
                existing.DeleteEvent = team.DeleteEvent;
                if (existing.CreateEvent == null)
                    existing.CreateEvent = team.CreateEvent;
                if (team.GroupId != null)
                {
                    existing.GroupId = team.GroupId;
                    existing.Url = team.Url;
                }

                await Courses.UpdateOneAsync(TeamFilter(env, id, existing.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1], existing));
            }
        }

        FilterDefinitionBuilder<CourseInfo> Filter => Builders<CourseInfo>.Filter;
        FilterDefinition<CourseInfo> TeamFilter(string env, int id, string teamId)
            => Filter.Where(c => c.CourseId == id && c.Environment == env) & Filter.ElemMatch(c => c.Teams, t => t.Id == teamId);

        public Task UpdateChannels(string env, int id, Team team)
            => Courses.UpdateOneAsync(TeamFilter(env, id, team.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1].Channels, team.Channels));

        public Task UpdateUsers(string env, int id, Team team)
            => Courses.UpdateOneAsync(TeamFilter(env, id, team.Id),
                    Builders<CourseInfo>.Update.Set(c => c.Teams[-1].Users, team.Users));
    }
}
