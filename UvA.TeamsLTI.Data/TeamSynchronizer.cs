﻿using DnsClient.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using UvA.Connectors.Teams;
using UvA.TeamsLTI.Data.Models;
using UvA.TeamsLTI.Services;
using Graph = Microsoft.Graph;

namespace UvA.TeamsLTI.Data
{
    public class TeamSynchronizer
    {
        TeamsData Data;
        ICourseService CourseService;
        ILogger<TeamSynchronizer> Logger;
        IConfiguration Config;

        public TeamSynchronizer(IConfiguration config, TeamsData data, ICourseService courseService, ILogger<TeamSynchronizer> log)
        {
            Config = config;
            Data = data;
            CourseService = courseService;
            Logger = log;
        }

        string Environment;
        int CourseId;
        Team Team;

        TeamsConnector Connector;
        string OwnerId, NicknamePrefix;

        public async Task Process(string env, int courseId, Team team)
        {
            Environment = env;
            CourseId = courseId;
            Team = team;

            var envSection = Config.GetSection("Environments").GetChildren().First(c => c["Authority"] == env);
            Connector = new TeamsConnector(Logger, Config.GetSection("Teams").GetSection(envSection["Teams"]));
            OwnerId = envSection["OwnerId"];
            NicknamePrefix = envSection["NicknamePrefix"];

            if (team.Contexts[0].Type == ContextType.Course)
                team.Contexts[0].Id = courseId;
            var res = await UpdateTeam();
            await CheckChannels();
            await UpdateChannels();
            await UpdateUsers();
            foreach (var channel in Team.Channels.Where(c => c.Contexts.Any() && c.Id != null))
                await UpdateChannelMembers(channel);
        }

        async Task<Graph.Team> UpdateTeam()
        {
            Graph.Team res;
            if (Team.GroupId == null)
            {
                res = await Connector.CreateTeam(Team.Name, $"{NicknamePrefix}-{Team.Contexts.First().Type.ToString().ToLower()}-{Team.Contexts.First().Id}",
                    Team.AllowChannels, Team.AllowPrivateChannels, new[] { OwnerId }, new string[0]);
                Team.GroupId = res.Id;
                Team.Url = res.WebUrl;
                await Data.UpdateTeam(Environment, CourseId, Team);
            }
            else
            {
                res = await Connector.GetTeam(Team.GroupId);
                if (res.DisplayName != Team.Name
                    || res.MemberSettings.AllowCreatePrivateChannels != Team.AllowPrivateChannels
                    || res.MemberSettings.AllowCreateUpdateChannels != Team.AllowChannels)
                {
                    // TODO: update
                }
            }
            return res;
        }

        async Task CheckChannels()
        {
            void checkChannel(ContextType type, int id, string name, int? groupSetId = null)
            {
                if (!Team.Channels.Any(c => c.Contexts.Any(z => z.Type == type && z.Id == id)))
                    Team.Channels = Team.Channels.Append(new Channel { Name = name, Contexts = new[] { new Context { Type = type, Id = id, GroupSetId = groupSetId } } }).ToArray();
            }

            var course = await CourseService.GetCourseInfo(CourseId);
            if (Team.CreateSectionChannels)
            {
                var sections = course.Sections.Where(s => Team.Contexts.Any(c => c.Type == ContextType.Course || c.Id == s.Id));
                foreach (var sec in sections)
                    checkChannel(ContextType.Section, sec.Id, sec.Name);
            }

            foreach (var id in Team.GroupSetIds)
            {
                var groups = await CourseService.GetGroups(CourseId, id);
                foreach (var group in groups)
                    checkChannel(ContextType.Group, group.Id, group.Name, id);
            }
        }

        async Task UpdateChannels()
        {
            foreach (var channel in Team.Channels.Where(c => c.Id == null).ToArray())
            {
                channel.Id = await Connector.CreatePrivateChannel(Team.GroupId, channel.Name, new[] { OwnerId }, new string[0]);
                await Data.UpdateChannels(Environment, CourseId, Team);
            }
        }

        async Task UpdateUsers()
        {
            var users = (await Task.WhenAll(Team.Contexts.Select(c => CourseService.GetUsers(CourseId, c)))).SelectMany(a => a).ToArray();
            var addedUsers = users.Where(u => !Team.Users.ContainsKey(u.Id.ToString())).ToArray();
            foreach (var user in addedUsers)
            {
                var gu = await Connector.FindUser(user.Email);
                if (gu == null)
                    Logger.LogError($"{CourseId}: can't find {user.Email}");
                else
                {
                    await Connector.AddMemberById(Team.GroupId, gu.Id);
                    Team.Users.Add(user.Id.ToString(), gu.Id);
                }
            }
            if (addedUsers.Any())
                await Data.UpdateUsers(Environment, CourseId, Team);

            var deletedUsers = Team.Users.Where(i => !users.Any(u => u.Id.ToString() == i.Key)).ToArray();
            foreach (var user in deletedUsers)
            {
                await Connector.RemoveTeamMember(Team.GroupId, user.Value);
                Team.Users.Remove(user.Key);
            }
            if (deletedUsers.Any())
                await Data.UpdateUsers(Environment, CourseId, Team);
        }

        async Task UpdateChannelMembers(Channel channel)
        {
            var users = (await Task.WhenAll(channel.Contexts.Select(c => CourseService.GetUsers(CourseId, c)))).SelectMany(a => a).ToArray();
            var addedUsers = users.Where(u => !channel.Users.ContainsKey(u.Id.ToString())).ToArray();

            foreach (var user in addedUsers.Where(u => Team.Users.ContainsKey(u.Id.ToString())))
            {
                var memId = await Connector.AddChannelMember(Team.GroupId, channel.Id, Team.Users[user.Id.ToString()]);
                if (memId != null)
                    channel.Users.Add(user.Id.ToString(), memId);
            }
            if (addedUsers.Any())
                await Data.UpdateChannels(Environment, CourseId, Team);

            var deletedUsers = channel.Users.Where(i => !users.Any(u => u.Id.ToString() == i.Key)).ToArray();
            foreach (var user in deletedUsers)
            {
                await Connector.RemoveChannelMember(Team.GroupId, channel.Id, user.Value);
                channel.Users.Remove(user.Key);
            }
            if (deletedUsers.Any())
                await Data.UpdateUsers(Environment, CourseId, Team);
        }
    }
}
