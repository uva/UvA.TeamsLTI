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
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Net.Http;
using System.Text;
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

            var config = Config.GetSection("Brightspace");
            services.AddAuthentication(opt =>
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
                })
                .AddOpenIdConnect(opt =>
                {
                    opt.Authority = config["Authority"] + "/";
                    opt.ClientId = config["ClientId"];
                    opt.ResponseMode = "form_post";
                    opt.AuthenticationMethod = OpenIdConnectRedirectBehavior.FormPost;
                    opt.Scope.Clear();
                    opt.Scope.Add("openid");
                    opt.Prompt = "none";
                    opt.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    opt.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = config["Endpoint"]
                    };
                    opt.TokenValidationParameters.IssuerSigningKeys = JsonWebKeySet.Create(new HttpClient().GetStringAsync(config["JwksUrl"]).Result).GetSigningKeys();
                    opt.TokenValidationParameters.ValidIssuer = config["Authority"];
                });

            services.AddTransient<ICourseService, BrightspaceService>();
            services.AddTransient<TeamsData>();
            services.AddTransient<TeamsConnector>();
            services.AddTransient<TeamSynchronizer>();

            services.AddAuthorization();
            services.AddControllers();
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
