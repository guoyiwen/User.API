using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Consul;
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
using Recommand.API.Infrastructure;
using ReCommand.API.EFData;
using ReCommand.API.IntergationEventHandlers;
using ReCommand.API.IntergationEvents;
using ReCommand.API.Options;
using ReCommand.API.Service;
using Resilience;
using zipkin4net;
using zipkin4net.Middleware;
using zipkin4net.Tracers.Zipkin;
using zipkin4net.Transport.Http;

namespace Recommand.API
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
            services.AddDbContext<ReCommandDbContext>(builder =>
            {

                builder.UseMySQL(Configuration.GetConnectionString("MysqlRecommand"), x =>
                {
                    x.MigrationsAssembly(typeof(Startup).GetTypeInfo().Assembly.GetName().Name);
                });

                //builder.UseMySQL(Configuration.GetConnectionString("MysqlProject"));
            });

            #region consul

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

            services.AddSingleton<IDnsQuery>(p =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDisvoveryOptions>>().Value;

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

            //services.AddTransient<ProjectCreatedIntergrationEventHandler>();

            services.AddScoped<IUserService, UserService>();
        
            services.AddScoped<IContactService,ContactService>();

          


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
                    d.CurrentNodePort = 57150;
                    d.NodeId = 4;
                    d.NodeName = "CAP No.4 Node ReCommand.API";
                });
            });

            #endregion


            services.AddMvc();

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Authority = "http://localhost:4000";
                    options.Audience = "recommand_api";
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env
            , ILoggerFactory loggerFactory,
            IApplicationLifetime applicationLifetime,
            IOptions<ServiceDisvoveryOptions> serviceOptions,
            IConsulClient consul)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }


            //启动时注册服务
            applicationLifetime.ApplicationStarted.Register(() =>
            {
                RegisterService(app, serviceOptions, consul);
            });

            //停助时移除服务
            applicationLifetime.ApplicationStopped.Register(() =>
            {
                DeRegisterService(app, serviceOptions, consul);
            });

            //RegisterZipkinTrace(app, loggerFactory, applicationLifetime);

            app.UseCap();

            app.UseAuthentication();

        

            app.UseMvc();
        }

        #region ConsulRegister
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

        #endregion

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

            app.UseTracing("recommand_api");

        }
    }
}
