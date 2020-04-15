using JoggingTimesAPI.Controllers;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace JoggingTimesAPI.Test.Controllers
{
    public class UserControllerTest
    {
        private GeneralValidations _validations;
        private MockBuilder _mockBuilder;
        private Mock<IUserService> _userService;
        private UserController _userController;

        public UserControllerTest()
        {
            _validations = new GeneralValidations();
            _mockBuilder = new MockBuilder();
            _userService = _mockBuilder.GenerateMockUserService();
            _userController = new UserController(
                _userService.Object,
                _mockBuilder.CreateMapper(),
                Options.Create(new AppSettings { Secret = "DontUseThisUnsafeSecret" }));
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestUserCanRegister()
        {
            _userService.Setup(s => s.Create(It.IsAny<User>())).ReturnsAsync(_mockBuilder.UserUser);

            var actionResult = await _userController.Register(new Models.UserRegisterModel
            {
                Username = _mockBuilder.UserUser.Username,
                Password = _mockBuilder.UserUser.NewPassword,
                EmailAddress = _mockBuilder.UserUser.EmailAddress
            });

            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                        { "Username", _mockBuilder.UserUser.Username },
                        { "NewPassword", _mockBuilder.UserUser.NewPassword },
                        { "EmailAddress", _mockBuilder.UserUser.EmailAddress },
                    });

            actionResult.ShouldBeOfType(typeof(OkObjectResult));
            var userResult = (User)(actionResult as OkObjectResult).Value;
            userResult.Username.ShouldBe(_mockBuilder.UserUser.Username);

            _userService.Verify(s => s.Create(It.IsAny<User>()), Times.Once);
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestOnlyValidUserCanAuthenticate()
        {
            _userService.Setup(s => s.Authenticate(_mockBuilder.UserUser.Username, _mockBuilder.UserUser.NewPassword))
                .ReturnsAsync(_mockBuilder.UserUser);

            var actionResult = await _userController.Authenticate(new Models.UserAuthenticateModel
            {
                Username = _mockBuilder.UserUser.Username,
                Password = _mockBuilder.UserUser.NewPassword
            });
            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "Username", _mockBuilder.UserUser.Username },
                    { "Role", _mockBuilder.UserUser.Role }
                });

            actionResult = await _userController.Authenticate(new Models.UserAuthenticateModel
            {
                Username = _mockBuilder.ManagerUser.Username,
                Password = _mockBuilder.ManagerUser.NewPassword
            });

            _validations.ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
            new Dictionary<string, object> {
                    { "message", "Invalid username and/or password." }
                });
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestGetOnErrorReturnsBadRequest()
        {
            _userService.Setup(s => s.GetByUsername(It.IsAny<string>(), null))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _userService.Setup(s => s.GetByUsername(It.IsAny<string>(), It.IsNotNull<User>()))
                .ReturnsAsync(_mockBuilder.UserUser);

            // On error, should return bad request
            var actionResult = await _userController.GetByName(_mockBuilder.UserUser.Username);
            _validations.ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.GetByName(_mockBuilder.UserUser.Username);
            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "Username", _mockBuilder.UserUser.Username },
                    { "NewPassword", _mockBuilder.UserUser.NewPassword },
                    { "Role", _mockBuilder.UserUser.Role },
                    { "EmailAddress", _mockBuilder.UserUser.EmailAddress },
                });
            _userController.AuthenticatedUser = null;
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestGetAllOnErrorReturnsBadRequest()
        {
            var userList = await _mockBuilder.GenerateMockUsers(10).Object.ToListAsync();

            _userService.Setup(s => s.GetAll(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), null))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _userService.Setup(s => s.GetAll(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsNotNull<User>()))
                .ReturnsAsync(userList);

            var model = new Models.GetAllModel();

            // On error, should return bad request
            var actionResult = await _userController.GetAll(model);
            _validations.ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.GetAll(model);
            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult), userList);
            _userController.AuthenticatedUser = null;
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestUpdateOnErrorReturnsBadRequest()
        {
            _userService.Setup(s => s.Update(It.IsAny<User>(), null))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _userService.Setup(s => s.Update(It.IsAny<User>(), It.IsNotNull<User>()))
                .ReturnsAsync(_mockBuilder.UserUser);

            var updateModel = new Models.UserUpdateModel
            {
                Username = _mockBuilder.UserUser.Username,
                Password = _mockBuilder.UserUser.NewPassword,
                Role = _mockBuilder.UserUser.Role,
                EmailAddress = _mockBuilder.UserUser.EmailAddress,
            };

            // On error, should return bad request
            var actionResult = await _userController.Update(updateModel);
            _validations.ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.Update(updateModel);
            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult),
            new Dictionary<string, object> {
                    { "Username", _mockBuilder.UserUser.Username },
                    { "NewPassword", _mockBuilder.UserUser.NewPassword },
                    { "Role", _mockBuilder.UserUser.Role },
                    { "EmailAddress", _mockBuilder.UserUser.EmailAddress },
                });
            _userController.AuthenticatedUser = null;
        }

        [Fact]
        [Trait("Actions", "User")]
        public async Task TestDeleteOnErrorShouldReturnBadRequest()
        {
            _userService.Setup(s => s.DeleteByUsername(It.IsAny<string>(), null))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _userService.Setup(s => s.DeleteByUsername(It.IsAny<string>(), It.IsNotNull<User>()))
                .ReturnsAsync(_mockBuilder.UserUser);

            // On error, should return bad request
            var actionResult = await _userController.DeleteByName(_mockBuilder.UserUser.Username);
            _validations.ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.DeleteByName(_mockBuilder.UserUser.Username);
            _validations.ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "Username", _mockBuilder.UserUser.Username },
                    { "NewPassword", _mockBuilder.UserUser.NewPassword },
                    { "Role", _mockBuilder.UserUser.Role },
                    { "EmailAddress", _mockBuilder.UserUser.EmailAddress },
                });
            _userController.AuthenticatedUser = null;
        }
    }
}