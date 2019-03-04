using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCore.CAP;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User.API.Data;
using User.API.IntergrationEvents;
using User.API.Models;

namespace User.API.Controllers
{
    [Route("api/users")]
    public class UsersController : BaseController
    {
        private readonly UserContext _userContext;
        private readonly ILogger<UsersController> _logger;
        private readonly ICapPublisher _capPublisher;

        public UsersController(UserContext userContext, ILogger<UsersController> logger, ICapPublisher capPublisher)
        {
            _userContext = userContext;
            _logger = logger;
            _capPublisher = capPublisher;
        }

        private async Task RasieUserInfoChangedEventAsyncTask(AppUser user)
        {
            if (_userContext.Entry(user).Property(x => x.Name).IsModified ||
                _userContext.Entry(user).Property(x => x.Company).IsModified ||
                _userContext.Entry(user).Property(x => x.Title).IsModified ||
                _userContext.Entry(user).Property(x => x.Phone).IsModified ||
                _userContext.Entry(user).Property(x => x.Avatar).IsModified)
            {
                var @event = new AppUserInfoChangedEvent()
                {
                    Avatar = user.Avatar,
                    Company = user.Company,
                    Id = user.Id,
                    Name = user.Name,
                    Phone = user.Phone,
                    Title = user.Title
                };
                await _capPublisher.PublishAsync<AppUserInfoChangedEvent>("finbook.userapi.userprofilechanged", @event);
            }
        }


        // GET api/values
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var user = await _userContext.Users
                  .AsNoTracking()
                  .Include(u => u.Properties)
                  .SingleOrDefaultAsync(u => u.Id == userIdentity.UserId);

            if (user == null)
                throw new UserOperationException($"错误的用户上下文Id : {userIdentity.UserId}");

            return Json(user);

        }

        [Route("")]
        [HttpPatch]
        public async Task<IActionResult> Patch([FromBody]JsonPatchDocument<AppUser> patch)
        {
            var user = await _userContext.Users
                .Include(u=>u.Properties)
                .SingleOrDefaultAsync(u => u.Id == userIdentity.UserId);
            patch.ApplyTo(user);

            if (user?.Properties != null)
            {
                foreach (var property in user?.Properties)
                {
                    _userContext.Entry(property).State = EntityState.Deleted;
                }


                var originProperties = await _userContext.UserProperties.AsNoTracking()
                    .Where(t => t.AppUserId == userIdentity.UserId).ToListAsync();
                var allProperties = originProperties.Union(user.Properties).Distinct();

                var removedPropoerties = originProperties.Except(user.Properties);
                var newPropoerties = allProperties.Except(originProperties);

                foreach (var property in removedPropoerties)
                {
                    _userContext.Remove(property);
                    //_userContext.Entry(property).State = EntityState.Deleted; 
                }

                foreach (var property in newPropoerties)
                {
                    _userContext.Add(property);
                    //_userContext.Entry(property).State = EntityState.Added;
                }
            }
          
            using (var trans = await _userContext.Database.BeginTransactionAsync())
            {
                await RasieUserInfoChangedEventAsyncTask(user);

                _userContext.Update(user);

                await _userContext.SaveChangesAsync();

                trans.Commit();
            }


            return Json(user);
        }

        /// <summary>
        /// 检查或者创建用户（当用户手机号不存在的时候则创建用户）
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        [Route("check-or-create")]
        [HttpPost]
        public async Task<IActionResult> CheckOrCreate(string phone)
        {

            //throw new Exception("就要错误");
            var user = await _userContext.Users.SingleOrDefaultAsync(u => u.Phone == phone);

            if (user == null)
            {
                user = new AppUser { Phone = phone };
                _userContext.Users.Add(user);
                await _userContext.SaveChangesAsync();
            }
            return Ok(new
            {
                user.Id,
                user.Name,
                user.Company,
                user.Phone,
                user.Title,
                user.Avatar
            });
        }

        /// <summary>
        /// 获取用户标签选项数据
        /// </summary>
        /// <returns></returns>
        [HttpGet("tags")]
        public async Task<IActionResult> GetUserTags()
        {
            return Ok(await _userContext.UserTags.Where(u => u.UserId == userIdentity.UserId).ToListAsync());
        }

        /// <summary>
        /// 根据手机号查询用户资料
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        [HttpPost("search")]
        public async Task<IActionResult> Search(string phone)
        {
            return Ok(await _userContext.Users.Include(u => u.Properties).SingleOrDefaultAsync(u => u.Id == userIdentity.UserId));
        }

        /// <summary>
        /// 更新用户标签数据
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>\
        [HttpPut("tags")]
        public async Task<IActionResult> UpdateUserTags([FromBody]List<string> tags)
        {
            var originTags = await _userContext.UserTags.Where(u => u.UserId == userIdentity.UserId).ToArrayAsync();
            var newTags = tags.Except(originTags.Select(t => t.Tag));
            await _userContext.UserTags.AddRangeAsync(newTags.Select(t => new UserTag
            {
                CreatedTime = DateTime.Now,
                UserId = userIdentity.UserId,
                Tag = t
            }));
            await _userContext.SaveChangesAsync();

            return Ok();
        }



        [HttpGet("baseinfo/{userId}")]
        public async Task<IActionResult> GetBaseInfo(int userId)
        {
            // TBD 检查用户是否好友关系

            var user = await _userContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new { user.Id, user.Name, user.Company, user.Title, user.Avatar });
        }





    }
}
