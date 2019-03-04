using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Resilience;
using User.Identity.Dtos;

namespace User.Identity.Services
{
    public class UserService : IUserService
    {
        private IHttpClient _httpClient;
        private string _userServiceUrl;
        private readonly ILogger<UserService> _logger;

        public UserService(IHttpClient httpClient, ILogger<UserService> logger, IOptions<ServiceDiscoveryOptions> serviceDiscoveryOptions, IDnsQuery dnsQuery)
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

        public async Task<UserInfo> CheckOrCreateAsync(string phone)
        {
            _logger.LogTrace($"Enter into  CheckOrCreate:{phone}");

            var form = new Dictionary<string, string>
            {
                {"phone",phone}
            };

            //var content = new FormUrlEncodedContent(form);
            try
            {
                var response = await _httpClient.PostAsync(_userServiceUrl + "api/users/check-or-create", form);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var result = await response.Content.ReadAsStringAsync();

                    var userInfo = JsonConvert.DeserializeObject<UserInfo>(result);


                    _logger.LogTrace($"Completed CheckOrCreate with userId:{userInfo.Id}");
                    return userInfo;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(" CheckOrCreateAsync 在重试后失败，" + ex.Message + ex.StackTrace);
                throw;
            }

            return null;
        }
    }
}
