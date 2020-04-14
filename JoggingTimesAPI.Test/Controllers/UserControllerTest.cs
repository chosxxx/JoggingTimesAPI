using JoggingTimesAPI.Controllers;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace JoggingTimesAPI.Test.Controllers
{
    public class UserControllerTest
    {
        private MockBuilder _mockBuilder;
        private Mock<IUserService> _userService;
        private UserController _userController;

        public UserControllerTest()
        {
            _mockBuilder = new MockBuilder();
            _userService = _mockBuilder.GenerateMockUserService();
            _userController = new UserController(
                _userService.Object,
                _mockBuilder.CreateMapper(),
                Options.Create(new AppSettings { Secret = "DontUseThisUnsafeSecret" }));
        }

        private void ValidateActionResult(IActionResult actionResult, Type expectedResultType, IDictionary<string, object> propertyValues)
        {
            actionResult.ShouldBeOfType(expectedResultType);
            var requestResult = (actionResult as ObjectResult).Value;

            foreach (var pv in propertyValues)
            {
                requestResult
                    .GetType()
                    .GetProperty(pv.Key)
                    .GetValue(requestResult)
                    .ShouldBe(pv.Value);
            }
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

            ValidateActionResult(actionResult, typeof(OkObjectResult),
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
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "Username", _mockBuilder.UserUser.Username },
                    { "Role", _mockBuilder.UserUser.Role }
                });

            actionResult = await _userController.Authenticate(new Models.UserAuthenticateModel
            {
                Username = _mockBuilder.ManagerUser.Username,
                Password = _mockBuilder.ManagerUser.NewPassword
            });

            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
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
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.GetByName(_mockBuilder.UserUser.Username);
            ValidateActionResult(actionResult, typeof(OkObjectResult),
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
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.Update(updateModel);
            ValidateActionResult(actionResult, typeof(OkObjectResult),
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
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return user data
            _userController.AuthenticatedUser = _mockBuilder.AdminUser;
            actionResult = await _userController.DeleteByName(_mockBuilder.UserUser.Username);
            ValidateActionResult(actionResult, typeof(OkObjectResult),
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