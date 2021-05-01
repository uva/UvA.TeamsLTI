using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UvA.TeamsLTI.Data.Models
{
    public class Team
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public string Name { get; set; }
        public Context[] Contexts { get; set; }
        public Channel[] Channels { get; set; }

        public string Url { get; set; }
        public string GroupId { get; set; }

        public bool AllowChannels { get; set; }
        public bool AllowPrivateChannels { get; set; }
        public bool CreateSectionChannels { get; set; }
        public int[] GroupSetIds { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> Users { get; set; } = new Dictionary<string, string>();
    }

    public class Channel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Context[] Contexts { get; set; }

        [JsonIgnore]
        public Dictionary<string, string> Users { get; set; } = new Dictionary<string, string>();
    }

    public class Context
    {
        public int Id { get; set; }
        public int? GroupSetId { get; set; }
        public ContextType Type { get; set; }
    }

    public enum ContextType
    {
        Course,
        Section,
        Group
    }
}
