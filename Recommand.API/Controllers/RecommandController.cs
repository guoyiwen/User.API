using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReCommand.API.EFData;
using ReCommand.API.EFData.Entities;
using ReCommand.API.IntergationEvents;
using ReCommand.API.Service;

namespace ReCommand.API.Controllers
{
    [Route("api/recommands")]
    public class RecommandController : BaseController
    {
        private readonly ReCommandDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly IContactService _contactService;


        public RecommandController(ReCommandDbContext dbContext, IUserService userService, IContactService contactService)
        {
            _dbContext = dbContext;
            _userService = userService;
            _contactService = contactService;
        }

        [HttpGet]
        [Route("projects")]
        public async Task<IActionResult> Get()
        {
            var projectReCommands = await _dbContext.ProjectReCommands.Include(x=>x.ProjectReferenceUsers)
                .Where(x=>x.UserId == UserIdentity.UserId).ToListAsync();
            return Ok(projectReCommands);
        }

      
        [CapSubscribe("projectapi.projectcreated")]
        [NonAction]
        public async Task Process(ProjectCreatedIntergrationEvent @event)
        {
            var info = await _userService.GetBaseUserInfoAsync(@event.UserId);
            var contacts = await _contactService.GetContactListByUserIdAsync(@event.UserId);
            foreach (var contact in contacts)
            {
                var projectRecommand = new ProjectReCommand()
                {
                    FromUserId = @event.UserId,
                    Company = @event.Company,
                    //Tags=@event.
                    ProjectId = @event.ProjectId,
                    Introduction = @event.Introduction,
                    EnumReCommandType = EnumReCommandType.Friend,
                    FromUserAvator = info.Avatar,
                    FromUserName = info.Name,
                    CreateTime = @event.CreationDate,
                    ReCommandTime = DateTime.Now,
                    UserId = contact.UserId,
                };
                await _dbContext.ProjectReCommands.AddAsync(projectRecommand);
            }
            await _dbContext.SaveChangesAsync();
        }
    }
}