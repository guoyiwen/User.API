using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Consul;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Project.API.Applications.Queries;
using Project.API.Applications.Services;
using Project.API.Options;
using Project.Domain.AggregatesModel;
using Project.Infrastructure;
using Project.Infrastructure.Repositories;
using zipkin4net;
using zipkin4net.Middleware;
using zipkin4net.Tracers.Zipkin;
using zipkin4net.Transport.Http;

namespace Project.API
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
            services.AddDbContext<ProjectDbContext>(options =>
            {
                options.UseMySQL(Configuration.GetConnectionString("UserMysqlLocal"), x =>
                {
                    x.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                });

                //options.UseMySQL(Configuration.GetConnectionString("UserMysqlLocal"));
            });

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Authority = "http://localhost:4000/";
                    options.Audience = "project_api";
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                });

            services.AddOptions();
            services.Configure<ServiceDisvoveryOptions>(Configuration.GetSection("ServiceDiscovery"));


            services.AddSingleton<IConsulClient>(p => new ConsulClient(cfg =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDisvoveryOptions>>().Value;

                if (!string.IsNullOrEmpty(serviceConfiguration.Consul.HttpEndpoint))
                {
                    // if not configured, the client will use the default value "127.0.0.1:8500"
                    cfg.Address = new Uri(serviceConfiguration.Consul.HttpEndpoint);
                }
            }));

             
            services.AddMediatR();

            services.AddScoped<ICommandService, TestCommandService>();
            services.AddScoped<IProjectQueries>(sp => new ProjectQueries(Configuration.GetConnectionString("MysqlLocal"), sp.GetRequiredService<ProjectDbContext>()));

            services.AddScoped<IProjectRepository, ProjectRepository>();

            services.AddCap(options =>
            {
                options.UseEntityFramework<ProjectDbContext>();
                //options.UseRabbitMQ(Configuration.GetConnectionString("RabbitMq"));
                options.UseRabbitMQ("localhost");

                // 注册 Dashboard
                options.UseDashboard();

                // 注册节点到 Consul
                options.UseDiscovery(d =>
                {
                    d.DiscoveryServerHostName = "localhost";
                    d.DiscoveryServerPort = 8500;
                    d.CurrentNodeHostName = "localhost";
                    d.CurrentNodePort = 61157;
                    d.NodeId = 3;
                    d.NodeName = "CAP No.3 Node Project.API";
                });
            });


            services.AddMvc().AddJsonOptions(x =>
            {
                x.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory, IHostingEnvironment env
            , IApplicationLifetime lifetime, IOptions<ServiceDisvoveryOptions> options, IConsulClient consulClient)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // 添加日志支持
             loggerFactory.AddConsole();
             loggerFactory.AddDebug();

            lifetime.ApplicationStarted.Register(() =>
            {
                RegisterService(app, options, consulClient);
            });

            lifetime.ApplicationStopped.Register(() =>
            {
                DeRegisterService(app, options, consulClient);
            });

            //RegisterZipkinTrace(app, loggerFactory, lifetime);

            app.UseCap();

            app.UseAuthentication();

            app.UseMvc();
        }

        private void DeRegisterService(IApplicationBuilder app, IOptions<ServiceDisvoveryOptions> serviceOptions, IConsulClient consul)
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

        private void RegisterService(IApplicationBuilder app, IOptions<ServiceDisvoveryOptions> serviceOptions, IConsulClient consul)
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


        private void RegisterZipkinTrace(IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            IApplicationLifetime lifetime)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                TraceManager.SamplingRate = 1.0f;
                var logger = new TracingLogger(loggerFactory, "zipkin4net");
                var httpSender = new HttpZipkinSender("http://127.0.0.1:9411", "application/json");
                var tracer = new ZipkinTracer(httpSender, new JSONSpanSerializer(), new Statistics());


                var consoleTracer = new zipkin4net.Tracers.ConsoleTracer();

                TraceManager.RegisterTracer(tracer);
                TraceManager.RegisterTracer(consoleTracer);
                TraceManager.Start(logger);

            });

            lifetime.ApplicationStopped.Register(() => TraceManager.Stop());

            app.UseTracing("project_api");

        }
    }
}
