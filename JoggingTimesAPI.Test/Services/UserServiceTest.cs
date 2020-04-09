using Xunit;
using Moq;
using JoggingTimesAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using System;
using JoggingTimesAPI.Services;
using System.Threading.Tasks;
using Shouldly;

namespace JoggingTimesAPI.Test.Services
{
    public class UserServiceTest
    {
        private MockBuilder _mockBuilder;
        private UserService _userService;
        private Mock<JoggingTimesDataContext> _dataContext;

        public UserServiceTest()
        {
            _mockBuilder = new MockBuilder();
            _dataContext = new Mock<JoggingTimesDataContext>(
                new DbContextOptionsBuilder<JoggingTimesDataContext>()
                .UseInMemoryDatabase("TestJogglingTimesDB").Options);
            _userService = new UserService(_dataContext.Object);
        }

        [Fact]
        public async Task TestValidUserAuthenticates()
        {
            var password = "NotASafePW";
            var userToAuthenticate = new User
            {
                Username = "Ccarrillo",
                NewPassword = password,
                Role = UserRole.Admin,
                EmailAddress = "myemail@mydomain.com"
            };
            userToAuthenticate.SetHashedPassword();

            var userList = _mockBuilder.GenerateMockUsers(10, userToAuthenticate);

            _dataContext.Setup(x => x.Users).Returns(userList);

            var authenticatedUser = await _userService.Authenticate(userToAuthenticate.Username, password);

            authenticatedUser.ShouldBeSameAs(userToAuthenticate);
        }

        [Fact]
        public async Task TestInValidUserShouldNotAuthenticate()
        {
            const string userName = "Ccarrillo";
            const string password = "NotASafePW";

            var userList = _mockBuilder.GenerateMockUsers(10);

            _dataContext.Setup(x => x.Users).Returns(userList);

            var authenticatedUser = await _userService.Authenticate(userName, password);

            authenticatedUser.ShouldBeNull();
        }
    }
}
