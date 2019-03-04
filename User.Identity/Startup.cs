using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DnsClient;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resilience;
using User.Identity.Authentication;
using User.Identity.Dtos;
using User.Identity.Infrastructure;
using User.Identity.Services;
using zipkin4net;
using zipkin4net.Middleware;
using zipkin4net.Tracers.Zipkin;
using zipkin4net.Transport.Http;

namespace User.Identity
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
            services.AddIdentityServer()
              .AddExtensionGrantValidator<Authentication.SmsAuthCodeValidator>()
              .AddDeveloperSigningCredential()
              .AddInMemoryClients(Config.GetClients())
              .AddInMemoryIdentityResources(Config.GetIdentityResources())
              .AddInMemoryApiResources(Config.GetApiResources());

            services.AddTransient<IProfileService, ProfileService>();

            #region consul

            services.Configure<ServiceDiscoveryOptions>(Configuration.GetSection("ServiceDiscovery"));
            services.AddSingleton<IDnsQuery>(p =>
            {
                var serviceConfiguration = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
                return new LookupClient(serviceConfiguration.Consul.DnsEndpoint.ToIPEndPoint());
            });

            //注册全局单例 ResilienceClientFactory
            services.AddSingleton(typeof(ResilienceClientFactory), sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ResilienceHttpClient>>();
                var httpcontextAccesser = sp.GetRequiredService<IHttpContextAccessor>();
                var retryCount = 5;
                var exceptionCountAllowedBeforeBreaking = 5;
                return new ResilienceClientFactory(logger, httpcontextAccesser, retryCount, exceptionCountAllowedBeforeBreaking);

            });

            //注册全局单例 IHttpClient
            services.AddSingleton<IHttpClient>(sp => sp.GetRequiredService<ResilienceClientFactory>().GetResilienceHttpClient());
            #endregion

            services.AddScoped<IAuthCodeService, TestAuthCodeService>();
            services.AddScoped<IUserService, UserService>();

            services.AddMvc();



        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env 
            , ILoggerFactory loggerFactory,
            IApplicationLifetime applicationLifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //RegisterZipkinTrace(app, loggerFactory,applicationLifetime);

            app.UseIdentityServer();

            app.UseMvc();
        }


        private void RegisterZipkinTrace(IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            IApplicationLifetime lifetime)
        {
            lifetime.ApplicationStarted.Register(() =>
            {
                TraceManager.SamplingRate = 1.0f;
                var logger = new TracingLogger(loggerFactory,"zipkin4net");
                var httpSender = new HttpZipkinSender("http://127.0.0.1:9411", "application/json");
                var tracer = new ZipkinTracer(httpSender, new JSONSpanSerializer(), new Statistics());


                var consoleTracer = new zipkin4net.Tracers.ConsoleTracer();

                TraceManager.RegisterTracer(tracer);
                TraceManager.RegisterTracer(consoleTracer);
                TraceManager.Start(logger);

            });

            lifetime.ApplicationStopped.Register(() => TraceManager.Stop());

            app.UseTracing("identity_api");

        }
    }
}
