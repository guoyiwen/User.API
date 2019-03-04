using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Contact.API.Data;
using Contact.API.Dtos;
using Contact.API.IntergrationEvents;
using Contact.API.Models;
using Contact.API.Services;
using Contact.API.ViewModel;
using DotNetCore.CAP;
using Microsoft.Extensions.Primitives;

namespace Contact.API.Controllers
{
    [Route("api/contacts")]
    public class ContactController : BaseController
    {

        private IContactApplyRequestRepository _contactApplyRequestRepository;
        private IContactRepository _contactRepository;
        private IUserService _userService;


        public ContactController(IContactApplyRequestRepository contactApplyRequestRepository
            , IUserService userService
            , IContactRepository contactRepository)
        {
            _contactApplyRequestRepository = contactApplyRequestRepository;
            _userService = userService;
            _contactRepository = contactRepository;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> Get(int userId, CancellationToken cancellationToken)
        {
            return Ok(await _contactRepository.GetContactsAsync(userId, cancellationToken));
        }

        [HttpPut("tag")]
        public async Task<IActionResult> TagContact([FromBody]TagContactInputViewModel viewModel, CancellationToken cancellationToken)
        {
            var result = await _contactRepository.TagContactAsync(UserIdentity.UserId, viewModel.ContactId, viewModel.Tags, cancellationToken);
            if (result)
            {
                return Ok();
            }
            //Log Tdb
            return BadRequest();

        }


        /// <summary>
        /// 获取添加好友申请列表
        /// </summary>
        /// <returns></returns>
        [HttpGet("apply-requests")]
        public async Task<IActionResult> GetApplyRequests(CancellationToken cancellationToken)
        {
            var requsert = await _contactApplyRequestRepository.GetRequsetListAsync(UserIdentity.UserId, cancellationToken);
            return Ok(requsert);
        }

        /// <summary>
        /// 添加好友请求
        /// </summary>
        /// <returns></returns>
        [HttpPost("apply-requests/{userId}")]
        public async Task<IActionResult> AddApplyRequest(int userId, CancellationToken cancellationToken)
        {
            var baseUserInfo = await _userService.GetBaseUserInfoAsync(userId);
            if (baseUserInfo == null)
            {
                throw new Exception("用户参数错误");
            }

            var result = await _contactApplyRequestRepository.AddReqeustAsync(new ContactApplyRequest
            {
                UserId = userId,
                ApplierId = UserIdentity.UserId,
                Name = baseUserInfo.Name,
                Company = baseUserInfo.Company,
                Title = baseUserInfo.Title,
                ApplyTime = DateTime.Now,
                Avatar = baseUserInfo.Avatar
            }, cancellationToken);

            if (!result)
            {
                //log tbd
                return BadRequest();
            }


            return Ok();


        }

        /// <summary>
        /// 通过好友请求
        /// </summary>
        /// <returns></returns>
        [HttpPut("apply-requests/{applierId}")]
        public async Task<IActionResult> ApprovalApplyRequest(int applierId, CancellationToken cancellationToken)
        {
            var result = await _contactApplyRequestRepository.ApprovalAsync(UserIdentity.UserId, applierId, cancellationToken);
            if (!result)
            {
                //log tbd
                return BadRequest();
            }

            var applier = await _userService.GetBaseUserInfoAsync(applierId);

            var userinfo = await _userService.GetBaseUserInfoAsync(UserIdentity.UserId);

            await _contactRepository.AddContactAsync(UserIdentity.UserId, applier, cancellationToken);

            await _contactRepository.AddContactAsync(applierId, userinfo, cancellationToken);


            return Ok();
        }

        [NonAction]
        [CapSubscribe("finbook.userapi.userprofilechanged")]
        public async Task ConsumerAppUserInfoChangedEvent(AppUserInfoChangedEvent @event)
        {
            await _contactRepository.UpdateContactInfoAsync(new BaseUserInfo
            {
                UserId = @event.Id,
                Avatar = @event.Avatar,
                Company = @event.Company,
                Name = @event.Name,
                Phone = @event.Phone,
                Title = @event.Title
            });
        }

    }
}