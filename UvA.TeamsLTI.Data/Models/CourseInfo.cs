using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace UvA.TeamsLTI.Data.Models
{
    public class CourseInfo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        public int CourseId { get; set; }
        public string Name { get; set; }
        public string Environment { get; set; }
        public Section[] Sections { get; set; }
        public GroupSet[] GroupSets { get; set; }
        public Team[] Teams { get; set; } = Array.Empty<Team>();

        [BsonIgnore]
        public string CourseUrl { get; set; }
    }

    public class GroupSet
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int GroupCount { get; set; }
    }

    public class Section
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
