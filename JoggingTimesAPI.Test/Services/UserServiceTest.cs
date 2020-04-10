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
using JoggingTimesAPI.Controllers;

namespace JoggingTimesAPI.Test.Services
{
    public class UserServiceTest
    {
        private const string unauthorizedErrorMessage = "User is unauthorized to perform this action.";
        private const string NotASafePW = "NotASafePW";
        private MockBuilder _mockBuilder;
        private UserService _userService;
        private Mock<JoggingTimesDataContext> _dataContext;

        #region Users to validate
        private User _userUser;
        private User _managerUser;
        private User _adminUser;

        private User UserUser
        {
            get
            {
                if (_userUser == null)
                    _userUser = new User
                    {
                        Username = "userUser",
                        NewPassword = NotASafePW,
                        Role = UserRole.User,
                        EmailAddress = "user@somedomain.com"
                    };
                return _userUser;
            }
        }

        private User ManagerUser
        {
            get
            {
                if (_managerUser == null)
                    _managerUser = new User
                    {
                        Username = "managerUser",
                        NewPassword = NotASafePW,
                        Role = UserRole.Manager,
                        EmailAddress = "manager@somedomain.com"
                    };
                return _managerUser;
            }
        }

        private User AdminUser
        {
            get
            {
                if (_adminUser == null)
                    _adminUser = new User
                    {
                        Username = "adminUser",
                        NewPassword = NotASafePW,
                        Role = UserRole.Admin,
                        EmailAddress = "admin@somedomain.com"
                    };
                return _adminUser;
            }
        }
        #endregion

        public UserServiceTest()
        {
            _mockBuilder = new MockBuilder();
            _dataContext = new Mock<JoggingTimesDataContext>(
                new DbContextOptionsBuilder<JoggingTimesDataContext>()
                .UseInMemoryDatabase("TestJogglingTimesDB").Options);
            _userService = new UserService(_dataContext.Object);
        }

        #region Test Anonymous
        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestValidUserAuthenticates()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User>{ UserUser });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            var authenticatedUser = await _userService.Authenticate(UserUser.Username, NotASafePW);

            authenticatedUser.ShouldBeSameAs(UserUser);
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestInValidUserShouldNotAuthenticate()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10);

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            var authenticatedUser = await _userService.Authenticate(ManagerUser.Username, NotASafePW);

            authenticatedUser.ShouldBeNull();
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestRegisterNewUserShouldBeUser()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            var returnedUser = await _userService.Create(AdminUser);

            userListMock.Verify(u => u.Add(AdminUser), Times.Once);
            returnedUser.Role.ShouldBe(UserRole.User);
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestRegisterExistingUserShouldFail()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser });
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            (await Should
                .ThrowAsync< ArgumentException>(_userService.Create(ManagerUser)))
                .Message.ShouldBe($"User {ManagerUser.Username} already exists.");
            userListMock.Verify(u => u.Add(AdminUser), Times.Never);
        }
        #endregion

        #region Test CRUD User
        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCanOnlyGetHimself()
        {
            var authenticatedUser = SimpleCopyUserFrom(UserUser, false);
            var anotherUser = SimpleCopyUserFrom(UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { UserUser, ManagerUser, AdminUser, anotherUser });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // User gets himself
            var returnedUser = await _userService.GetByUsername(UserUser.Username, authenticatedUser);
            returnedUser.ShouldBeSameAs(UserUser);

            // User cannot get another user
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(anotherUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // User cannot get a manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(ManagerUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // User cannot get an admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(AdminUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
        }

        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCannotCreateOtherUsers()
        {
            var authenticatedUser = SimpleCopyUserFrom(UserUser, false);
            var anotherUser = SimpleCopyUserFrom(UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // User cannot create other users
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Create(anotherUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Add(anotherUser), Times.Never);
        }

        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCanUpdateOnlyHimselfButNotChangeRole()
        {
            var authenticatedUser = SimpleCopyUserFrom(UserUser, false);
            var anotherUser = SimpleCopyUserFrom(UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, anotherUser });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // An User cannot change his own role
            UserUser.Role = UserRole.Manager;
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(UserUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(UserUser), Times.Never);
            UserUser.Role = UserRole.User;

            // An User cannot update another User
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(anotherUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(anotherUser), Times.Never);

            // An User cannot update a Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(ManagerUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(ManagerUser), Times.Never);

            // An User can update himself (no Role)
            UserUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(UserUser, authenticatedUser);
            returnedUser.ShouldBeSameAs(UserUser);
            userListMock.Verify(u => u.Update(UserUser), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCanDeleteOnlyHimself()
        {
            var authenticatedUser = SimpleCopyUserFrom(UserUser, false);
            var anotherUser = SimpleCopyUserFrom(UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, anotherUser });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // User cannot delete another User
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(anotherUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(anotherUser), Times.Never);

            // User cannot delete a Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(ManagerUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(ManagerUser), Times.Never);
            // User can delete himself
            var returnedUser = await _userService.DeleteByUsername(UserUser.Username, authenticatedUser);
            userListMock.Verify(u => u.Remove(UserUser), Times.Once);
        }
        #endregion

        #region Test CRUD Manager
        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanGetUsersAndHimself()
        {
            var authenticatedManager = SimpleCopyUserFrom(ManagerUser, false);
            var anotherManager = SimpleCopyUserFrom(ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { UserUser, ManagerUser, AdminUser, anotherManager });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager can get himself
            var returnedUser = await _userService.GetByUsername(ManagerUser.Username, authenticatedManager);
            returnedUser.ShouldBeSameAs(ManagerUser);

            // Manager can get another user
            returnedUser = await _userService.GetByUsername(UserUser.Username, authenticatedManager);
            returnedUser.ShouldBeSameAs(UserUser);

            // Manager cannot get another manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(anotherManager.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // Manager cannot get an admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(AdminUser.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanOnlyCreateUsers()
        {
            var authenticatedManager = SimpleCopyUserFrom(ManagerUser, false);
            var anotherManager = SimpleCopyUserFrom(ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager can create Users
            var returnedUser = await _userService.Create(UserUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(UserUser);
            userListMock.Verify(u => u.Add(UserUser), Times.Once);

            // Manager cannot create other managers
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Create(anotherManager, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Add(anotherManager), Times.Never);

            // Manager cannot create admins
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Create(AdminUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Add(AdminUser), Times.Never);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanUpdateUsersAndHimselfButNotChangeRole()
        {
            var authenticatedManager = SimpleCopyUserFrom(ManagerUser, false);
            var anotherManager = SimpleCopyUserFrom(ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, AdminUser, anotherManager });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // A Manager cannot change his role
            ManagerUser.Role = UserRole.Admin;
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(ManagerUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(ManagerUser), Times.Never);
            ManagerUser.Role = UserRole.Manager;

            // A Manager cannot update another Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(anotherManager, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(anotherManager), Times.Never);

            // A Manager cannot update an Admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(AdminUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(AdminUser), Times.Never);

            // A Manager can update himself
            ManagerUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(ManagerUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(ManagerUser);
            userListMock.Verify(u => u.Update(ManagerUser), Times.Once);

            // A Manager can update an User
            UserUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(UserUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(UserUser);
            userListMock.Verify(u => u.Update(UserUser), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanDeleteUsersAndHimself()
        {
            var authenticatedManager = SimpleCopyUserFrom(ManagerUser, false);
            var anotherManager = SimpleCopyUserFrom(ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, AdminUser, anotherManager });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager cannot delete another Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(anotherManager.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(anotherManager), Times.Never);

            // Manager cannot delete an Admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(AdminUser.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(AdminUser), Times.Never);

            // Manager can delete an User
            var returnedUser = await _userService.DeleteByUsername(UserUser.Username, authenticatedManager);
            userListMock.Verify(u => u.Remove(UserUser), Times.Once);

            // Manager can delete himself
            returnedUser = await _userService.DeleteByUsername(ManagerUser.Username, authenticatedManager);
            userListMock.Verify(u => u.Remove(ManagerUser), Times.Once);
        }
        #endregion

        #region Test CRUD Admin
        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanGetAnyone()
        {
            var authenticatedAdmin = SimpleCopyUserFrom(AdminUser, false);
            var anotherAdmin = SimpleCopyUserFrom(AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { UserUser, ManagerUser, AdminUser, anotherAdmin });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can get anyone, including other Admins
            var returnedUser = await _userService.GetByUsername(AdminUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(AdminUser);

            returnedUser = await _userService.GetByUsername(UserUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(UserUser);

            returnedUser = await _userService.GetByUsername(ManagerUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(ManagerUser);

            returnedUser = await _userService.GetByUsername(anotherAdmin.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(anotherAdmin);
        }

        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanCreateAnyone()
        {
            var authenticatedAdmin = SimpleCopyUserFrom(AdminUser, false);
            var anotherAdmin = SimpleCopyUserFrom(AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can create Users
            var returnedUser = await _userService.Create(UserUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(UserUser);
            userListMock.Verify(u => u.Add(UserUser), Times.Once);

            // Admin can create Managers
            returnedUser = await _userService.Create(ManagerUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(ManagerUser);
            userListMock.Verify(u => u.Add(ManagerUser), Times.Once);

            // Admin can create other Admins
            returnedUser = await _userService.Create(anotherAdmin, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(anotherAdmin);
            userListMock.Verify(u => u.Add(anotherAdmin), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanUpdateAnyone()
        {
            var authenticatedAdmin = SimpleCopyUserFrom(AdminUser, false);
            var anotherAdmin = SimpleCopyUserFrom(AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, AdminUser, anotherAdmin });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // An Admin can update himself
            AdminUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(AdminUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(AdminUser);
            userListMock.Verify(u => u.Update(AdminUser), Times.Once);

            // An Admin can update an User
            UserUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(UserUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(UserUser);
            userListMock.Verify(u => u.Update(UserUser), Times.Once);

            // An Admin can update a Manager
            ManagerUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(ManagerUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(ManagerUser);
            userListMock.Verify(u => u.Update(ManagerUser), Times.Once);

            // An Admin can update another Admin
            anotherAdmin.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(anotherAdmin, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(anotherAdmin);
            userListMock.Verify(u => u.Update(anotherAdmin), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanDeleteAnyone()
        {
            var authenticatedAdmin = SimpleCopyUserFrom(AdminUser, false);
            var anotherAdmin = SimpleCopyUserFrom(AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { ManagerUser, UserUser, AdminUser, anotherAdmin });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can delete an User
            var returnedUser = await _userService.DeleteByUsername(UserUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(UserUser), Times.Once);

            // Admin can delete a Manager
            returnedUser = await _userService.DeleteByUsername(ManagerUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(ManagerUser), Times.Once);

            // Admin can delete himself
            returnedUser = await _userService.DeleteByUsername(AdminUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(AdminUser), Times.Once);

            // Admin can delete another Admin
            returnedUser = await _userService.DeleteByUsername(anotherAdmin.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(anotherAdmin), Times.Once);
        }
        #endregion

        /// <summary>
        /// Creates a copy of the user only with the basic information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private User SimpleCopyUserFrom(User user, bool unique)
        {
            return new User
            {
                Username = unique ? $"{user.Username}_Copy" : user.Username,
                Role = user.Role
            };
        }
    }
}
