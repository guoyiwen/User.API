using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contact.API.Dtos;

namespace Contact.API.Services
{
  public interface IUserService
  {


    /// <summary>
    /// 获取用户基本信息
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<BaseUserInfo> GetBaseUserInfoAsync(int userId);


  }
}
