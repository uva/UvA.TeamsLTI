using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UvA.Connectors.Teams;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Data.Models;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.TestTool
{
    class Program
    {
        public static ILogger<T> CreateLogger<T>()
        {
            var sp = new ServiceCollection().AddLogging(b => b.AddConsole()).BuildServiceProvider();
            return sp.GetService<ILoggerFactory>().CreateLogger<T>();
        }

        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().AddUserSecrets("0e890c86-7b61-4452-8297-7688efddefdb").Build();
            MigrateCanvas(config).Wait();
        }

        static async Task MigrateCanvas(IConfiguration config)
        {
            var data = new TeamsData(config);
            var conn = new TeamsConnector(null, config.GetSection("Teams"));

            var cnv = new CanvasService(config.GetSection("Canvas"));
            var log = CreateLogger<Program>();

            var groups = await conn.GetGroups($"startsWith(mailNickname, 'canvas-')", false);
            var env = "https://canvas.uva.nl";
            foreach (var group in groups.GroupBy(g => int.Parse(g.MailNickname.Split('-')[1])))
            {
                if (await data.GetCourse(env, group.Key) != null)
                    continue;
                CourseInfo info;

                try
                {
                    info = await cnv.GetCourseInfo(group.Key);
                }
                catch (Exception ex)
                {
                    log.LogError($"Failed to get course info for {group.Key}: {ex.Message}");
                    continue;
                }

                var cvgroups = info.GroupSets.SelectMany(g => cnv.GetGroups(group.Key, g.Id).Result).ToArray();

                info.Teams = await Task.WhenAll(group.Select(async team =>
                {
                    var gt = await conn.GetTeam(team.Id);
                    var md = await conn.GetMetadata(team.Id, "UvA.TeamCreator.ChannelMapping");
                    return new Team
                    {
                        Name = team.DisplayName,
                        GroupId = team.Id,
                        AllowChannels = gt.MemberSettings.AllowCreateUpdateChannels == true,
                        AllowPrivateChannels = gt.MemberSettings.AllowCreatePrivateChannels == true,
                        Contexts = md.ContainsKey("SectionIds") ? JArray.Parse(md["SectionIds"].ToString()).ToObject<int[]>().Select(sec => new Context { Id = sec, Type = ContextType.Section }).ToArray()
                            : new[] { new Context { Id = group.Key, Type = ContextType.Course } },
                        GroupSetIds = md.ContainsKey("GroupSetIds") ? JArray.Parse(md["GroupSetIds"].ToString().TrimStart('{').TrimEnd('}')).ToObject<int[]>() : new int[0],
                        CreateSectionChannels = md.Keys.Any(k => k.StartsWith("section-")),
                        CreateEvent = new Event { Date = DateTime.Now, DateExecuted = DateTime.Now, User = "<import>" },
                        Url = gt.WebUrl,
                        Channels = md.Where(e => e.Key.StartsWith("section-") || e.Key.StartsWith("group-")).Select(e =>
                        {
                            var id = int.Parse(e.Key.Split('-')[1]);
                            var name = e.Key.StartsWith("section") ? info.Sections.FirstOrDefault(s => s.Id == id)?.Name : cvgroups.FirstOrDefault(g => g.Id == id)?.Name;
                            return new Channel
                            {
                                Name = name,
                                Id = e.Value.ToString(),
                                Contexts = new [] { new Context { Id = id, Type = e.Key.StartsWith("section") ? ContextType.Section : ContextType.Group } }
                            };
                        }).ToArray()
                    };
                }));
                info.Name = cnv.GetName(group.Key);
                info.Environment = env;
                info.EndDate = cnv.GetEndDate(group.Key);
                if (info.EndDate == null)
                    log.LogWarning($"No end date for {group.Key}: {info.Name}");

                await data.UpdateCourseInfo(info);
            }
        }

        static void ConvertConfig(string path)
        {
            var content = JObject.Parse(File.ReadAllText(path));
            
            IEnumerable<JObject> process(JObject obj, string prefix)
            {
                return obj.Properties().SelectMany(p =>
                {
                    var name = prefix == "" ? p.Name : $"{prefix}__{p.Name}";
                    if (p.Value is JObject jo)
                        return process(jo, name);
                    else
                        return new[] 
                        { 
                            JObject.FromObject(new
                            {
                                name = name,
                                value = p.Value.ToString(),
                                slotSetting = false
                            })
                        };
                });
            }
            Console.WriteLine(new JArray(process(content, "")).ToString());
        }
    }
}
