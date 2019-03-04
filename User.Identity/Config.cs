using System.Collections.Generic;
using System.Security.Claims;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Test;

namespace User.Identity
{
    public class Config
    {
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
          {
              new ApiResource("user_api", "user service"),
              new ApiResource("gateway_api", "gateway service"),
              new ApiResource("project_api", "project service"),
              new ApiResource("contact_api", "contact service"),
              new ApiResource("recommand_api", "recommand service")
          };
        }

        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new Client
                {
                    ClientId = "android",
                    ClientSecrets = new List<Secret>
                    {
                      new Secret("secret".Sha256())
                    },

                    RefreshTokenExpiration=TokenExpiration.Sliding,
                    AllowOfflineAccess=true,
                    RequireClientSecret=false,
                    AllowedGrantTypes = new List<string>{ "sms_auth_code" },
                    AlwaysIncludeUserClaimsInIdToken=true,
                    AllowedScopes =
                    {
                      "user_api",
                      "gateway_api",
                      "project_api",
                      "contact_api",
                      "recommand_api",
                      IdentityServerConstants.StandardScopes.OfflineAccess,
                      IdentityServerConstants.StandardScopes.OpenId,
                      IdentityServerConstants.StandardScopes.Profile,

                    }

                }

              #region 说明
               
                //,  new Client
                //{
                //    //标明这个客户端是否启用，默认为true
                //    Enabled=false, 
                //    //唯一的客户编号
                //    ClientId = "test",
                //    //客户端密钥列表---只有在需要密钥的处理流(flow)中使用。
                //    ClientSecrets =
                //    {
                //        new Secret("secret".Sha256())
                //    },
                //    //客户端显示名(用于授权页面和日志服务)
                //    ClientName="客户端显示名",
                //    //关于客户端的详细信息网页 (在授权页面上使用)
                //    ClientUri="",
                //    //客户端的Logo(在授权页面上使用)
                //    LogoUri="",
                //    //指定是否需要用户明确授权，默认为 true
                //    RequireConsent=false,
                //    //指定是否可以记录用户授权决定。默认为true.
                //    AllowRememberConsent=false,
                //    //指定登出时可以重定向的URIs
                //    PostLogoutRedirectUris=
                //    {

                //    },
                //    //指定客户端是否可以通过浏览器请求访问令牌。
                //    // 这个可以强化多返回类型的处理流程(比如: 禁止混合处理流程客户端(应该只使用code id_token）
                //    //使用token响应类型防止令牌泄露到浏览器。限制它只能使用code id_token)
                //    AllowAccessTokensViaBrowser   =false,
                //    IdentityTokenLifetime=0,
                //    UpdateAccessTokenClaimsOnRefresh=false,
                //    AllowedGrantTypes = GrantTypes.ClientCredentials,

                //    RefreshTokenExpiration=TokenExpiration.Sliding,
                //    AbsoluteRefreshTokenLifetime=0,


                //    AllowedScopes = { "api1" }
                //}
              #endregion
        };
        }

        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
          {
              new IdentityResources.OpenId(),
              new IdentityResources.Profile(),
              new IdentityResources.Email()
          };
        }


        public static List<TestUser> GetTestUsers()
        {
            return new List<TestUser>
          {
              new TestUser
              {
                  SubjectId = "10000",
                  Username = "evan",
                  Password = "password",
                  Claims=new List<Claim>
                  {
                      new Claim("name","evan"),
                      new Claim("website","testlocalhost")
                  }

              }
          };
        }
    }
}