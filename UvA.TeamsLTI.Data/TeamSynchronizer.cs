using DnsClient.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
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
        CourseInfo Course;

        IConfigurationSection TeamsConfig;
        TeamsConnector _Connector;

        TeamsConnector Connector
            => _Connector ?? (_Connector = new TeamsConnector(Logger, TeamsConfig));

        string OwnerId, NicknamePrefix;

        void LoadConfig(string env)
        {
            var envSection = Config.GetSection("Environments").GetChildren().First(c => c["Host"] == env);
            TeamsConfig = Config.GetSection("Teams").GetSection(envSection["Teams"]);
            OwnerId = envSection["OwnerId"];
            NicknamePrefix = envSection["NicknamePrefix"];
        }

        public async Task Process(string env, int courseId, Team team, bool batch = false)
        {
            Environment = env;
            CourseId = courseId;
            Team = team;

            LoadConfig(env);

            if (team.DeleteEvent != null)
            {
                if (team.GroupId != null)
                {
                    try
                    {
                        await Connector.DeleteGroup(team.GroupId);
                    }
                    catch (Graph.ServiceException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // already gone
                    }
                }
                team.DeleteEvent.DateExecuted = DateTime.Now;
                await Data.UpdateTeam(Environment, CourseId, team);
                return;
            }
            Course = await CourseService.GetCourseInfo(CourseId);

            if (team.Contexts[0].Type == ContextType.Course)
                team.Contexts[0].Id = courseId;
            if (!batch)
                await UpdateTeam();
            await CheckChannels();
            if (await UpdateUsers() | await UpdateChannels())
                await Task.Delay(TimeSpan.FromSeconds(30)); // need to wait before adding users to new channels
            foreach (var channel in Team.Channels.Where(c => c.Contexts.Any() && c.Id != null))
                await UpdateChannelMembers(channel);
        }

        public async Task AddOwner(string env, Team team, string uid)
        {
            LoadConfig(env);
            var user = await Connector.FindUser(uid);
            await Connector.AddOwnerById(team.GroupId, user.Id);
            foreach (var channel in team.Channels.Where(c => !string.IsNullOrEmpty(c.Id)))
            {
                var cur = await Connector.GetChannelMembers(team.GroupId, channel.Id);
                var mem = cur.FirstOrDefault(c => c.UserId == user.Id);
                if (mem != null)
                    await Connector.ChangeChannelMemberRole(team.GroupId, channel.Id, mem.Id, true);
            }
        }

        public async Task Clean(string env, int courseId, Team team)
        {
            foreach (var chan in team.Channels)
                chan.Users.Clear();
            await Data.UpdateChannels(env, courseId, team);
        }

        async Task<Graph.Team> UpdateTeam()
        {
            Graph.Team res;
            if (Team.GroupId == null)
            {
                var nickname = $"{NicknamePrefix}-{CourseId}";
                if (Team.Contexts.First().Type != ContextType.Course)
                    nickname += $"-{Team.Contexts.First().Type.ToString().ToLower()}-{Team.Contexts.First().Id}";
                nickname += $"-{Team.Id}";
                res = await Connector.CreateTeam(Team.Name, nickname.Length > 64 ? nickname.Substring(0, 64) : nickname,
                    Team.AllowChannels, Team.AllowPrivateChannels, new[] { OwnerId }, new string[0]);
                Team.GroupId = res.Id;
                Team.Url = res.WebUrl;
                Team.CreateEvent.DateExecuted = DateTime.Now;
                await Data.UpdateTeam(Environment, CourseId, Team);

                if (Course.CourseUrl != null)
                {
                    string channelId = null;
                    for (int i = 0; true; i++)
                    {
                        try
                        {
                            channelId = (await Connector.GetChannel(res.Id, "General")).Id;
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (i >= 10)
                            {
                                Logger.LogError(ex, $"Failed to find channel for team {res.Id}");
                                break;
                            }
                            await Task.Delay(5000 * i);
                        }
                    }
                    if (channelId != null)
                        await Connector.AddTab(res.Id, channelId, "Course", Course.CourseUrl);
                }
                var triggerUrl = TeamsConfig["TriggerUrl"];
                if (!string.IsNullOrEmpty(triggerUrl))
                {
                    try
                    {
                        await new HttpClient().PostAsJsonAsync(triggerUrl, new
                        {
                            TeamProperties = new
                            {
                                TeamID = Team.GroupId
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, $"Error while calling trigger for {Team.Name}");
                    }
                }
            }
            else
            {
                res = await Connector.GetTeam(Team.GroupId);
                if (res.DisplayName != Team.Name)
                    await Connector.UpdateGroupName(Team.GroupId, Team.Name);
                if (res.MemberSettings.AllowCreatePrivateChannels != Team.AllowPrivateChannels
                    || res.MemberSettings.AllowCreateUpdateChannels != Team.AllowChannels)
                    await Connector.UpdateTeamSettings(Team.GroupId, Team.AllowChannels, Team.AllowPrivateChannels);
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

            if (Team.CreateSectionChannels)
            {
                var sections = Course.Sections.Where(s => Team.Contexts.Any(c => c.Type == ContextType.Course || c.Id == s.Id));
                foreach (var sec in sections)
                    checkChannel(ContextType.Section, sec.Id, sec.Name);
            }

            foreach (var id in Team.GroupSetIds)
            {
                var groups = await CourseService.GetGroups(CourseId, id);
                foreach (var group in groups)
                {
                    var name = group.Name;
                    if (Team.GroupSetIds.Length > 1)
                        group.Name += $" ({Course.GroupSets.First(s => s.Id == id).Name})";
                    checkChannel(ContextType.Group, group.Id, group.Name, id);
                }
            }
        }

        async Task<bool> UpdateChannels()
        {
            var newChannels = Team.Channels.Any(c => c.Id == null);
            foreach (var channel in Team.Channels.Where(c => c.Id == null).ToArray())
            {
                if (Team.Channels.Any(c => c.Name == channel.Name && c.Id != null))
                    channel.Name += channel.Contexts.Any(c => c.GroupSetId != null) ? $" ({Course.GroupSets.First(s => s.Id == channel.Contexts.First().GroupSetId).Name})" : " (1)";
                channel.Id = await Connector.CreatePrivateChannel(Team.GroupId, channel.Name, new[] { OwnerId }, new string[0]);
                await Data.UpdateChannels(Environment, CourseId, Team);
            }
            return newChannels;
        }

        async Task<bool> UpdateUsers()
        {
            var users = (await Task.WhenAll(Team.Contexts.Select(c => CourseService.GetUsers(CourseId, c)))).SelectMany(a => a)
                .Where(u => u.Email != null).Distinct().ToArray();
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

            return addedUsers.Any();
        }

        async Task UpdateChannelMembers(Channel channel)
        {
            var users = (await Task.WhenAll(channel.Contexts.Select(c => CourseService.GetUsers(CourseId, c)))).SelectMany(a => a).Distinct().ToArray();
            var addedUsers = users.Where(u => !channel.Users.ContainsKey(u.Id.ToString())).ToArray();

            foreach (var user in addedUsers.Where(u => Team.Users.ContainsKey(u.Id.ToString())))
            {
                var memId = await Connector.AddChannelMember(Team.GroupId, channel.Id, Team.Users[user.Id.ToString()]);
                if (memId != null)
                    channel.Users.Add(user.Id.ToString(), memId);
                else
                {
                    // already in there?
                    var cur = await Connector.GetChannelMembers(Team.GroupId, channel.Id);
                    var mem = cur.FirstOrDefault(c => c.UserId == Team.Users[user.Id.ToString()]);
                    if (mem != null)
                        channel.Users.Add(user.Id.ToString(), memId);
                }
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
                await Data.UpdateChannels(Environment, CourseId, Team);
        }
    }
}
