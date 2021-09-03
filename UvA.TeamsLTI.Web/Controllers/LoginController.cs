using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UvA.TeamsLTI.Web.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        public const string Student = "Student";
        public const string Teacher = "Teacher";

        string JwtKey;
        IConfiguration Environments;

        public LoginController(IConfiguration config)
        {
            JwtKey = config["Jwt:Key"];
            Environments = config.GetSection("Environments");
        }

        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public IActionResult Get()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var context = JsonDocument.Parse(User.FindFirst("https://purl.imsglobal.org/spec/lti/claim/context").Value).RootElement;
            var roles = User.FindAll("https://purl.imsglobal.org/spec/lti/claim/roles").Select(c => c.Value);

            var courseId = context.GetProperty("id").GetString();
            if (!int.TryParse(courseId, out _))
                courseId = CustomClaim.GetProperty("courseid").ToString();

            var aut = User.Claims.First().Issuer;
            var cid = User.FindFirstValue("aud");
            var env = Environments.GetChildren().Where(e => e["Authority"] == aut);
            if (env.Count() > 1)
                env = env.Where(e => e["ClientId"] == cid);

            var token = new JwtSecurityToken("lti",
              "lti",
              new[]
              {
                  new Claim("courseId", courseId),
                  new Claim("courseName", context.GetProperty("title").GetString()),
                  new Claim(ClaimTypes.Role, roles.Any(e => e.Contains("Instructor")) ? Teacher : Student),
                  new Claim("environment", env.Single()["Host"]),
                  new Claim("authority", aut),
                  new Claim("clientId", cid),
                  new Claim(ClaimTypes.Email, User.FindFirstValue(ClaimTypes.Email)),
                  new Claim(ClaimTypes.NameIdentifier, User.FindFirstValue(ClaimTypes.NameIdentifier).Split("_").Last())
              },
              expires: DateTime.Now.AddMinutes(120),
              signingCredentials: credentials);

            var enc = new JwtSecurityTokenHandler().WriteToken(token);

            return Redirect($"/#{enc}");
        }

        JsonElement CustomClaim => JsonDocument.Parse(User.FindFirst("https://purl.imsglobal.org/spec/lti/claim/custom").Value).RootElement;
    }
}
