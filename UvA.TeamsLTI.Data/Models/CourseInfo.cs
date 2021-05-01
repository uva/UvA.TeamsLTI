using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UvA.TeamsLTI.Data.Models
{
    public class CourseInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Section[] Sections { get; set; }
        public GroupSet[] GroupSets { get; set; }
        public Team[] Teams { get; set; }
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
