using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using User.API.Data;
using AspNet.Security.OAuth.Weixin;
using Consul;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using User.API.Dtos;
using User.API.Filters;

namespace User.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<UserContext>(options =>
            {
                options.UseMySQL(Configuration.GetConnectionString("MysqlUser"));
            });

            services.AddCap(options =>
            {
                options.UseEntityFramework<UserContext>();
                options.UseRabbitMQ(Configuration.GetConnectionString("RabbitMq"));
//                options.UseRabbitMQ("localhost");

                // 注册 Dashboard
                options.UseDashboard();

                // 注册节点到 Consul
                options.UseDiscovery(d =>
                {
                    d.DiscoveryServerHostName = "localhost";
                    d.DiscoveryServerPort = 8500;
                    d.CurrentNodeHostName = "localhost";
                    d.CurrentNodePort = 5001;
                    d.NodeId = 1;
                    d.NodeName = "CAP No.1 Node User.API";
                });
            });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
             .AddCookie(options =>
                {
                    options.LoginPath = "/login";
                    options.LogoutPath = "/signout";
                })
             .AddWeixin(options =>
                {
                    options.ClientId = "wxe455e60c8d40d6d4";
                    options.ClientSecret = "3463ef8c67c3be382f81368cf781524a";

                });
            services.AddOptions();
            services.Configure<ServiceDiscoveryOptions>(Configuration.GetSection("ServiceDiscovery"));

            services.AddSingleton<IConsulClient>(p => new ConsulClient(cfg =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

                if (!string.IsNullOrEmpty(serviceConfiguration.Consul.HttpEndpoint))
                {
                    // if not configured, the client will use the default value "127.0.0.1:8500"
                    cfg.Address = new Uri(serviceConfiguration.Consul.HttpEndpoint);
                }
            }));


            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "http://localhost:4000";
                    options.Audience = "user_api";
                    options.RequireHttpsMetadata = false;
                });



            services.AddMvc(options =>
            {
                options.Filters.Add(typeof(GlobalExeceptionFilter));
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
          ILoggerFactory loggerFactory,
          IApplicationLifetime applicationLifetime,
          IOptions<ServiceDiscoveryOptions> serviceOptions,
          IConsulClient consul)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

//            //启动时注册服务
//            applicationLifetime.ApplicationStarted.Register(() =>
//            {
//                RegisterService(app, serviceOptions, consul);
//            });
//
//            //停助时移除服务
//            applicationLifetime.ApplicationStopped.Register(() =>
//            {
//                DeRegisterService(app, serviceOptions, consul);
//            });

            app.UseCap();

            app.UseAuthentication();

            app.UseMvc();
//            UserContextSeed.SeedAsync(app, loggerFactory).Wait();
//            InitUserDatabase(app);

        }

        private void DeRegisterService(IApplicationBuilder app, IOptions<ServiceDiscoveryOptions> serviceOptions, IConsulClient consul)
        {
            var features = app.Properties["server.Features"] as FeatureCollection;
            var addresses = features.Get<IServerAddressesFeature>()
              .Addresses
              .Select(p => new Uri(p));

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.Value.ServiceName}_{address.Host}:{address.Port}";



                consul.Agent.ServiceDeregister(serviceId).GetAwaiter().GetResult();


            }
        }

        private void RegisterService(IApplicationBuilder app, IOptions<ServiceDiscoveryOptions> serviceOptions, IConsulClient consul)
        {
            var features = app.Properties["server.Features"] as FeatureCollection;
            var addresses = features.Get<IServerAddressesFeature>()
              .Addresses
              .Select(p => new Uri(p));

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.Value.ServiceName}_{address.Host}:{address.Port}";

                var httpCheck = new AgentServiceCheck()
                {
                    DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                    Interval = TimeSpan.FromSeconds(10),
                    HTTP = new Uri(address, "HealthCheck").OriginalString
                };

                var registration = new AgentServiceRegistration()
                {
                    Checks = new[] { httpCheck },
                    Address = address.Host,
                    ID = serviceId,
                    Name = serviceOptions.Value.ServiceName,
                    Port = address.Port
                };

                consul.Agent.ServiceRegister(registration).GetAwaiter().GetResult();


            }

        }


        private void InitUserDatabase(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var userContext = scope.ServiceProvider.GetRequiredService<UserContext>();

                userContext.Database.EnsureCreated();//针对当前访问的上下文对象，如果数据库中存在该表，则不做修改；否则的话进行创建
                if (!userContext.Users.Any())
                {
                    userContext.Users.Add(new Models.AppUser { Name = "evan" });
                    userContext.SaveChanges();
                }
            }
        }

    }
}
