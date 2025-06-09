using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UvA.LTI;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Services;
using UvA.TeamsLTI.Web.Controllers;
using UvA.TeamsLTI.Web.Services;

namespace UvA.TeamsLTI.Web
{
    public class Startup(IConfiguration config)
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder
                    .WithOrigins("http://localhost:8080")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddAuthentication(opt =>
                {
                    opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(opt =>
                {
                    var key = Encoding.UTF8.GetBytes(config["Jwt:Key"]);

                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });

            services.AddHttpContextAccessor();
            services.AddTransient<ICourseService>(sp =>
            {
                var acc = sp.GetRequiredService<IHttpContextAccessor>();
                var aut = acc.HttpContext.User.FindFirstValue("authority");
                var clientId = acc.HttpContext.User.FindFirstValue("clientId");
                var configs = sp.GetRequiredService<IConfiguration>().GetSection("Environments").GetChildren().Where(c => c["Authority"] == aut);
                if (configs.Count() > 1)
                    configs = configs.Where(c => c["ClientId"] == clientId);
                if (!configs.Any())
                    throw new Exception($"Cannot find {aut}/{clientId}");
                var config = configs.Single();
                return aut.Contains("canvas") ? new CanvasService(config) : new BrightspaceService(config);
            });
            services.AddTransient<TeamsData>();
            services.AddTransient<TeamSynchronizer>();

            services.AddTransient<TeamSynchronizerResolver>();
            services.AddTransient<SyncEngine>();
            services.AddHostedService<SyncService>();
            services.AddHostedService<CleanupService>();

            services.AddAuthorization();
            services.AddControllers();
        }

        class ConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
        {
            public IConfiguration Config;
            OpenIdConnectConfiguration Current;
            DateTimeOffset? ExpiresOn;

            public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
            {
                if (Current == null || ExpiresOn < DateTimeOffset.Now.AddMinutes(5))
                {
                    Current = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = Config["Endpoint"]
                    };
                    var keySet = JsonWebKeySet.Create(await new HttpClient().GetStringAsync(Config["JwksUrl"]));
                    if (keySet.Keys.All(k => k.AdditionalData.ContainsKey("exp")))
                        ExpiresOn = keySet.Keys.Select(k => DateTimeOffset.FromUnixTimeSeconds((long)k.AdditionalData["exp"])).Min();
                    foreach (var key in keySet.GetSigningKeys())
                        Current.SigningKeys.Add(key);
                }
                return Current;
            }

            public void RequestRefresh()
            {
                Current = null;
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("CorsPolicy");

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "dist")) });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "dist")) });
            
            app.UseForwardedHeaders();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            foreach (var section in config.GetSection("Environments").GetChildren())
            {
                var clientId = section["ClientId"];
                app.UseLti(new LtiOptions
                {
                    AuthenticateUrl = section["Endpoint"],
                    ClientId = clientId,
                    InitiationEndpoint = "oidc",
                    LoginEndpoint = "signin-oidc",
                    SigningKey = config["Jwt:Key"],
                    JwksUrl = section["JwksUrl"],
                    RedirectUrl = "",
                    ClaimsMapping = p => new Dictionary<string, object>
                    {
                        ["courseId"] = int.TryParse(p.Context.Id, out _) ? p.Context.Id 
                            : p.CustomClaims?.GetProperty("courseid").ToString(),
                        ["courseName"] = p.Context.Title,
                        [ClaimTypes.Role] = p.Roles.Any(e => e.Contains("http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"))
                            ? LoginController.Manager : p.Roles.Any(e => e.Contains("Instructor")) 
                                ? LoginController.Teacher : LoginController.Student,
                        ["environment"] = section["Host"],
                        [ClaimTypes.Email] = p.Email,
                        [ClaimTypes.NameIdentifier] = p.CustomClaims?.TryGetProperty("userid", out var el) == true 
                            ? int.Parse(el.ToString()) : p.NameIdentifier.Split("_").Last(),
                        ["authority"] = section["Authority"],
                        ["clientId"] = clientId
                    }
                });
            }
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
