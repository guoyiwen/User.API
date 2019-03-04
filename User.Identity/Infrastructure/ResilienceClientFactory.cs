using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Resilience;


namespace User.Identity.Infrastructure
{
    public class ResilienceClientFactory
    {

        private ILogger<ResilienceHttpClient> _logger;

        private IHttpContextAccessor _httpContextAccessor;
        //重试次数
        private int _retryCount;
        //熔断之前允许的异常次数
        private int _exceptionCountAllowedBeforeBreaking;

        public ResilienceClientFactory(ILogger<ResilienceHttpClient> logger
            , IHttpContextAccessor httpContextAccessor
            , int retryCount
            , int exceptionCountAllowedBeforeBreaking)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _retryCount = retryCount;
            _exceptionCountAllowedBeforeBreaking = exceptionCountAllowedBeforeBreaking;
        }


        public ResilienceHttpClient GetResilienceHttpClient() =>
          new ResilienceHttpClient("identity_api", origin => CreatePolicy(origin), _logger, _httpContextAccessor);


        private Policy[] CreatePolicy(string origin)
        {
            return new Policy[]
            {
        Policy.Handle<HttpRequestException>()
          .WaitAndRetryAsync(
            // number of retries
            _retryCount,
            // exponential backofff
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            // on retry
            (exception, timeSpan, retryCount, context) =>
            {
              var msg = $"第 {retryCount} 次重试 " +
                        $"of {context.PolicyKey} " +
                        $"at {context.ExecutionKey}, " +
                        $"due to: {exception}.";
              _logger.LogWarning(msg);
              _logger.LogDebug(msg);
            }),
        Policy.Handle<HttpRequestException>()
                .CircuitBreakerAsync( 
                   // number of exceptions before breaking circuit
                   _exceptionCountAllowedBeforeBreaking,
                   // time circuit opened before retry
                   TimeSpan.FromMinutes(1),
                   (exception, duration) =>
                   {
                        // on circuit opened
                        _logger.LogWarning("熔断器打开");
                   },
                   () =>
                   {
                        // on circuit closed
                        _logger.LogWarning("熔断器关闭");
                   })

            };


        }



    }
}
