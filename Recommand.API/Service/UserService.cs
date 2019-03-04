using System;
using System.Linq;
using System.Threading.Tasks;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReCommand.API.Dtos;
using ReCommand.API.Options;
using Resilience;

namespace ReCommand.API.Service
{
    public class UserService : IUserService
    {
        private readonly IHttpClient _httpClient;
        private readonly ILogger<UserService> _logger;
        private readonly string _userServiceUrl;

        public UserService(IHttpClient httpClient, ILogger<UserService> logger, IOptions<ServiceDisvoveryOptions> serviceDiscoveryOptions, IDnsQuery dnsQuery)
        {
            _httpClient = httpClient;
            _logger = logger;

            var address = dnsQuery.ResolveService("service.consul",
                serviceDiscoveryOptions.Value.UserServiceName);
            var addressList = address.First().AddressList;
            var host = addressList.Any() ? addressList.First().ToString() : address.First().HostName.Replace(".", "");
            var port = address.First().Port;

            _userServiceUrl = $"http://{host}:{port}/";

            //_userServiceUrl = "http://localhost:5001/";

        }

        public async Task<BaseUserInfo> GetBaseUserInfoAsync(int userId)
        {
            BaseUserInfo result = null;
            try
            {
                var reponse = await _httpClient.GetStringAsync($"{_userServiceUrl}api/users/baseinfo/{userId}");
                if (!string.IsNullOrWhiteSpace(reponse))
                {
                    result = JsonConvert.DeserializeObject<BaseUserInfo>(reponse);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "调用Http服务BaseInfo失败");
            }
            return result;
        }
    }
}