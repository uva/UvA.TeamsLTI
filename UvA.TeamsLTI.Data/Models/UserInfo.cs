using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UvA.TeamsLTI.Data.Models
{
    public record UserInfo
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public bool IsTeacher { get; internal set; }

        public override string ToString() => $"{Id}: {Email}";
    }
}
