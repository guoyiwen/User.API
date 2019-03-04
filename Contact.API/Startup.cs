using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Consul;
using Contact.API.Common;
using Contact.API.Data;
using Contact.API.Dtos;
using Contact.API.Filters;
using Contact.API.Infrastructure;
using Contact.API.Services;
using DnsClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using DotNetCore.CAP;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Resilience;

namespace Contact.API
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

            services.AddOptions();

            services.Configure<AppSettings>(x =>
            {
                x.MongoContactConnectionString = Configuration["MongoContactConnectionString"].ToString();
                x.MongoDbDatabase = Configuration["MongoDbDataBase"].ToString();
            });

     

            #region consul

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

            services.AddSingleton<IDnsQuery>(p =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

                return new LookupClient(serviceConfiguration.Consul.DnsEndpoint.ToIPEndPoint());
            });

             services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            //注册全局单例 ResilienceClientFactory
            services.AddSingleton(typeof(ResilienceClientFactory), sp =>
            {
                var loggger = sp.GetRequiredService<ILogger<ResilienceClientFactory>>();
                var logggerHttpClinet = sp.GetRequiredService<ILogger<ResilienceHttpClient>>();
                var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                var retryCount = 5;
                var expCountAllowedBeforeBreak = 5;
                var factory = new ResilienceClientFactory(loggger, httpContextAccessor, retryCount, expCountAllowedBeforeBreak, logggerHttpClinet);
                return factory;
            });




            //注册全局单例 IHttpClient
            services.AddSingleton<IHttpClient>(sp => sp.GetRequiredService<ResilienceClientFactory>().GetResilienceHttpClient());
            #endregion

            services.AddScoped<MongoContactDbContext>();


            services.AddScoped<IContactApplyRequestRepository, MongoContactApplyRequestRepository>();

            services.AddScoped<IContactRepository, MongoContactRepository>();

            services.AddScoped<IUserService, UserService>();

            services.AddLogging(x => x.AddConsole());

            #region Cap
             
            services.AddCap(options =>
            {
                options.UseMySql(Configuration.GetConnectionString("UserMysqlLocal"));
                //options.UseRabbitMQ(Configuration.GetConnectionString("RabbitMq"));
                options.UseRabbitMQ("localhost");
                //启用仪表盘
                options.UseDashboard();
                //Register to Consul
                options.UseDiscovery(d =>
                {
                    d.DiscoveryServerHostName = "localhost";
                    d.DiscoveryServerPort = 8500;
                    d.CurrentNodeHostName = "localhost";
                    d.CurrentNodePort = 56580;
                    d.NodeId = 2;
                    d.NodeName = "CAP No.2 Node Contact.API";
                });
            });

            #endregion

            services.AddMvc(options =>
            {
                 options.Filters.Add(typeof(GlobalExeceptionFilter));
            });

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Authority = "http://localhost:4000/";
                    options.Audience = "contact_api";
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime, IOptions<ServiceDiscoveryOptions> options, IConsulClient consulClient)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            lifetime.ApplicationStarted.Register(() =>
            {
                RegisterService(app, options, consulClient);
            });

            lifetime.ApplicationStopped.Register(() =>
            {
                DeRegisterService(app, options, consulClient);
            });

            app.UseCap();

            app.UseAuthentication();

            app.UseMvc();
        }

        private void DeRegisterService(IApplicationBuilder app, IOptions<ServiceDiscoveryOptions> serviceOptions, IConsulClient consul)
        {
            var features = app.Properties["server.Features"] as FeatureCollection;
            var addresses = features.Get<IServerAddressesFeature>()
                .Addresses
                .Select(p => new Uri(p));

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.Value.ContactServiceName}_{address.Host}:{address.Port}";



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
                var serviceId = $"{serviceOptions.Value.ContactServiceName}_{address.Host}:{address.Port}";

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
                    Name = serviceOptions.Value.ContactServiceName,
                    Port = address.Port
                };

                consul.Agent.ServiceRegister(registration).GetAwaiter().GetResult();


            }

        }
    }
}
