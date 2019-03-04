using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCore.CAP;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using User.API.Controllers;
using User.API.Data;
using User.API.Models;
using Xunit;

namespace User.API.UnitTests
{
    public class UserControllerUnitTests
    {
        private UserContext GetUserContext()
        {
            var options = new DbContextOptionsBuilder<UserContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var userContext = new UserContext(options);

            userContext.Users.Add(new AppUser { Id = 1, Name = "Evan" });

            userContext.SaveChanges();

            return userContext;

        } 

        private (UsersController controller, UserContext userContext) GetUsersController()
        {
            var context = GetUserContext();

            var loggerMoq = new Mock<ILogger<UsersController>>();
            var logger = loggerMoq.Object;
            var publisher = new Mock<ICapPublisher>().Object;
            return (controller: new UsersController(context, logger, publisher), userContext: context);
        }


        /// <summary>
        /// 三段  ，主体是谁，测试结果应该是什么 ，参数
        /// </summary>
        [Fact]
        public async Task Get_ReturnRigthUser_WithExpectedParaeters()
        {
            (UsersController controller, UserContext _) = GetUsersController();

            var response = await controller.Get();

            var result = response.Should().BeOfType<JsonResult>().Subject;
            var appUser = result.Value.Should().BeAssignableTo<AppUser>().Subject;
            appUser.Id.Should().Be(1);
            appUser.Name.Should().Be("Evan");

            //Assert.IsType<JsonResult>(response);
        }
 
        [Fact]
        public async Task Patch_ReturnNewName_WithExpectedNewNameParameter()
        {
            (UsersController controller, UserContext userContext) = GetUsersController();

            var document = new JsonPatchDocument<AppUser>();
            document.Replace(u => u.Name, "lei");
            var response = await controller.Patch(document);
            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert response
            var appUser = result.Value.Should().BeAssignableTo<AppUser>().Subject;
            appUser.Name.Should().Be("lei");

            //assert name value in ef context
            var userModel = await userContext.Users.SingleOrDefaultAsync(u => u.Id == 1);
            userModel.Should().NotBeNull();
            userModel.Name.Should().Be("lei");

        }

        [Fact]
        public async Task Patch_ReturnNewProperties_WithExpectedNewProperty()
        {
            (UsersController controller, UserContext userContext) = GetUsersController();

            var document = new JsonPatchDocument<AppUser>();
            document.Replace(u => u.Properties, new List<UserProperty>
            {
                new UserProperty{Key="fin_industry",Value="互联网",Text="互联网" }
            });
            var response = await controller.Patch(document);
            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert response
            var appUser = result.Value.Should().BeAssignableTo<AppUser>().Subject;
            appUser.Properties.Count.Should().Be(1);
            appUser.Properties.First().Value.Should().Be("互联网");
            appUser.Properties.First().Key.Should().Be("fin_industry");

            //assert name value in ef context
            var userModel = await userContext.Users.SingleOrDefaultAsync(u => u.Id == 1);
            userModel.Properties.Count.Should().Be(1);
            userModel.Properties.First().Value.Should().Be("互联网");
            userModel.Properties.First().Key.Should().Be("fin_industry");

        }

        [Fact]
        public async Task Patch_ReturnNewProperties_WithRemoveProperty()
        {
            (UsersController controller, UserContext userContext) = GetUsersController();

            var document = new JsonPatchDocument<AppUser>();
            document.Replace(u => u.Properties, new List<UserProperty>());
            var response = await controller.Patch(document);
            var result = response.Should().BeOfType<JsonResult>().Subject;

            //assert response
            var appUser = result.Value.Should().BeAssignableTo<AppUser>().Subject;
            appUser.Properties.Should().BeEmpty();

            //assert name value in ef context
            var userModel = await userContext.Users.SingleOrDefaultAsync(u => u.Id == 1);
            userModel.Properties.Should().BeEmpty();

        }

    }
}
