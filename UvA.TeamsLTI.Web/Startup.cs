using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UvA.TeamCreator.Shared;
using UvA.TeamsLTI.Data;
using UvA.TeamsLTI.Services;

namespace UvA.TeamsLTI.Web
{
    public class Startup
    {
        IConfiguration Config;

        public Startup(IConfiguration config)
        {
            Config = config;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder
                    .WithOrigins("http://localhost:8081")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            var auth = services.AddAuthentication(opt =>
                {
                    opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(opt =>
                {
                    opt.Cookie.SameSite = SameSiteMode.None;
                })
                .AddJwtBearer(opt =>
                {
                    var key = Encoding.UTF8.GetBytes(Config["Jwt:Key"]);

                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
            foreach (var config in Config.GetSection("Environments").GetChildren())
            {
                auth.AddOpenIdConnect(config.Key, opt =>
                {
                    opt.Authority = config["Authority"] + "/";
                    opt.ClientId = config["ClientId"];
                    opt.ResponseMode = "form_post";
                    opt.AuthenticationMethod = OpenIdConnectRedirectBehavior.FormPost;
                    opt.Scope.Clear();
                    opt.Scope.Add("openid");
                    opt.Prompt = "none";
                    opt.SkipUnrecognizedRequests = true;
                    opt.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    opt.Configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = config["Endpoint"]
                    };
                    opt.ConfigurationManager = new ConfigurationManager { Config = config };
                    opt.TokenValidationParameters.ValidIssuer = config["Authority"];
                });
            }

            services.AddHttpContextAccessor();
            services.AddTransient<ICourseService>(sp =>
            {
                var acc = sp.GetRequiredService<IHttpContextAccessor>();
                var env = acc.HttpContext.User.FindFirstValue("environment");
                var config = sp.GetRequiredService<IConfiguration>();
                return env.Contains("canvas") ? new CanvasService(config) : new BrightspaceService(config);
            });
            services.AddTransient<TeamsData>();
            services.AddTransient<TeamsConnector>();
            services.AddTransient<TeamSynchronizer>();

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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
