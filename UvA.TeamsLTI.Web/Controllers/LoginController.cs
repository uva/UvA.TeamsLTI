﻿using Microsoft.AspNetCore.Authentication.Cookies;
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
        string JwtKey;

        public LoginController(IConfiguration config)
        {
            JwtKey = config["Jwt:Key"];
        }

        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        public IActionResult Get()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var context = JsonDocument.Parse(User.FindFirst("https://purl.imsglobal.org/spec/lti/claim/context").Value).RootElement;

            var token = new JwtSecurityToken("lti",
              "lti",
              new[]
              {
                  new Claim("courseId", context.GetProperty("id").GetString()),
                  new Claim("courseName", context.GetProperty("title").GetString())
              },
              expires: DateTime.Now.AddMinutes(120),
              signingCredentials: credentials);

            var enc = new JwtSecurityTokenHandler().WriteToken(token);

            return Redirect($"/#{enc}");
        }
    }
}
