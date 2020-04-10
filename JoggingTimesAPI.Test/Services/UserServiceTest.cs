﻿using Xunit;
using Moq;
using JoggingTimesAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using JoggingTimesAPI.Services;
using System.Threading.Tasks;
using Shouldly;

namespace JoggingTimesAPI.Test.Services
{
    public class UserServiceTest
    {
        private const string unauthorizedErrorMessage = "User is unauthorized to perform this action.";
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

        #region Test Anonymous
        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestValidUserAuthenticates()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User>{ _mockBuilder.UserUser });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            var authenticatedUser = await _userService.Authenticate(_mockBuilder.UserUser.Username, _mockBuilder.UserUser.NewPassword);

            authenticatedUser.ShouldBeSameAs(_mockBuilder.UserUser);
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestInValidUserShouldNotAuthenticate()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10);

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            var authenticatedUser = await _userService.Authenticate(_mockBuilder.ManagerUser.Username, _mockBuilder.ManagerUser.NewPassword);

            authenticatedUser.ShouldBeNull();
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestRegisterNewUserShouldBeUser()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            var returnedUser = await _userService.Create(_mockBuilder.AdminUser);

            userListMock.Verify(u => u.Add(_mockBuilder.AdminUser), Times.Once);
            returnedUser.Role.ShouldBe(UserRole.User);
        }

        [Fact]
        [Trait("CRUD", "Anonymous")]
        public async Task TestRegisterExistingUserShouldFail()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(5, new List<User> { _mockBuilder.ManagerUser });
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            (await Should
                .ThrowAsync< ArgumentException>(_userService.Create(_mockBuilder.ManagerUser)))
                .Message.ShouldBe($"User {_mockBuilder.ManagerUser.Username} already exists.");
            userListMock.Verify(u => u.Add(_mockBuilder.AdminUser), Times.Never);
        }
        #endregion

        #region Test CRUD User
        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCanOnlyGetHimself()
        {
            var authenticatedUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, false);
            var anotherUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, 
                new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser, _mockBuilder.AdminUser, anotherUser });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // User gets himself
            var returnedUser = await _userService.GetByUsername(_mockBuilder.UserUser.Username, authenticatedUser);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);

            // User cannot get another user
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(anotherUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // User cannot get a manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(_mockBuilder.ManagerUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // User cannot get an admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(_mockBuilder.AdminUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
        }

        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCannotCreateOtherUsers()
        {
            var authenticatedUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, false);
            var anotherUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);

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
            var authenticatedUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, false);
            var anotherUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, anotherUser });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // An User cannot change his own role
            _mockBuilder.UserUser.Role = UserRole.Manager;
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(_mockBuilder.UserUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(_mockBuilder.UserUser), Times.Never);
            _mockBuilder.UserUser.Role = UserRole.User;

            // An User cannot update another User
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(anotherUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(anotherUser), Times.Never);

            // An User cannot update a Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(_mockBuilder.ManagerUser, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(_mockBuilder.ManagerUser), Times.Never);

            // An User can update himself (no Role)
            _mockBuilder.UserUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(_mockBuilder.UserUser, authenticatedUser);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);
            userListMock.Verify(u => u.Update(_mockBuilder.UserUser), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "User")]
        public async Task TestUserCanDeleteOnlyHimself()
        {
            var authenticatedUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, false);
            var anotherUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, anotherUser });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // User cannot delete another User
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(anotherUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(anotherUser), Times.Never);

            // User cannot delete a Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(_mockBuilder.ManagerUser.Username, authenticatedUser)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(_mockBuilder.ManagerUser), Times.Never);
            // User can delete himself
            var returnedUser = await _userService.DeleteByUsername(_mockBuilder.UserUser.Username, authenticatedUser);
            userListMock.Verify(u => u.Remove(_mockBuilder.UserUser), Times.Once);
        }
        #endregion

        #region Test CRUD Manager
        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanGetUsersAndHimself()
        {
            var authenticatedManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, false);
            var anotherManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, 
                new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser, _mockBuilder.AdminUser, anotherManager });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager can get himself
            var returnedUser = await _userService.GetByUsername(_mockBuilder.ManagerUser.Username, authenticatedManager);
            returnedUser.ShouldBeSameAs(_mockBuilder.ManagerUser);

            // Manager can get another user
            returnedUser = await _userService.GetByUsername(_mockBuilder.UserUser.Username, authenticatedManager);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);

            // Manager cannot get another manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(anotherManager.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);

            // Manager cannot get an admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.GetByUsername(_mockBuilder.AdminUser.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanOnlyCreateUsers()
        {
            var authenticatedManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, false);
            var anotherManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager can create Users
            var returnedUser = await _userService.Create(_mockBuilder.UserUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);
            userListMock.Verify(u => u.Add(_mockBuilder.UserUser), Times.Once);

            // Manager cannot create other managers
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Create(anotherManager, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Add(anotherManager), Times.Never);

            // Manager cannot create admins
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Create(_mockBuilder.AdminUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Add(_mockBuilder.AdminUser), Times.Never);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanUpdateUsersAndHimselfButNotChangeRole()
        {
            var authenticatedManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, false);
            var anotherManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, _mockBuilder.AdminUser, anotherManager });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // A Manager cannot change his role
            _mockBuilder.ManagerUser.Role = UserRole.Admin;
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(_mockBuilder.ManagerUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(_mockBuilder.ManagerUser), Times.Never);
            _mockBuilder.ManagerUser.Role = UserRole.Manager;

            // A Manager cannot update another Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(anotherManager, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(anotherManager), Times.Never);

            // A Manager cannot update an Admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.Update(_mockBuilder.AdminUser, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Update(_mockBuilder.AdminUser), Times.Never);

            // A Manager can update himself
            _mockBuilder.ManagerUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(_mockBuilder.ManagerUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(_mockBuilder.ManagerUser);
            userListMock.Verify(u => u.Update(_mockBuilder.ManagerUser), Times.Once);

            // A Manager can update an User
            _mockBuilder.UserUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(_mockBuilder.UserUser, authenticatedManager);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);
            userListMock.Verify(u => u.Update(_mockBuilder.UserUser), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "Manager")]
        public async Task TestManagerCanDeleteUsersAndHimself()
        {
            var authenticatedManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, false);
            var anotherManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, _mockBuilder.AdminUser, anotherManager });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Manager cannot delete another Manager
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(anotherManager.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(anotherManager), Times.Never);

            // Manager cannot delete an Admin
            (await Should
                .ThrowAsync<InvalidOperationException>(_userService.DeleteByUsername(_mockBuilder.AdminUser.Username, authenticatedManager)))
                .Message.ShouldBe(unauthorizedErrorMessage);
            userListMock.Verify(u => u.Remove(_mockBuilder.AdminUser), Times.Never);

            // Manager can delete an User
            var returnedUser = await _userService.DeleteByUsername(_mockBuilder.UserUser.Username, authenticatedManager);
            userListMock.Verify(u => u.Remove(_mockBuilder.UserUser), Times.Once);

            // Manager can delete himself
            returnedUser = await _userService.DeleteByUsername(_mockBuilder.ManagerUser.Username, authenticatedManager);
            userListMock.Verify(u => u.Remove(_mockBuilder.ManagerUser), Times.Once);
        }
        #endregion

        #region Test CRUD Admin
        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanGetAnyone()
        {
            var authenticatedAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, false);
            var anotherAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, 
                new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser, _mockBuilder.AdminUser, anotherAdmin });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can get anyone, including other Admins
            var returnedUser = await _userService.GetByUsername(_mockBuilder.AdminUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.AdminUser);

            returnedUser = await _userService.GetByUsername(_mockBuilder.UserUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);

            returnedUser = await _userService.GetByUsername(_mockBuilder.ManagerUser.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.ManagerUser);

            returnedUser = await _userService.GetByUsername(anotherAdmin.Username, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(anotherAdmin);
        }

        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanCreateAnyone()
        {
            var authenticatedAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, false);
            var anotherAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5);
            userListMock.Setup(u => u.Add(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can create Users
            var returnedUser = await _userService.Create(_mockBuilder.UserUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);
            userListMock.Verify(u => u.Add(_mockBuilder.UserUser), Times.Once);

            // Admin can create Managers
            returnedUser = await _userService.Create(_mockBuilder.ManagerUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.ManagerUser);
            userListMock.Verify(u => u.Add(_mockBuilder.ManagerUser), Times.Once);

            // Admin can create other Admins
            returnedUser = await _userService.Create(anotherAdmin, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(anotherAdmin);
            userListMock.Verify(u => u.Add(anotherAdmin), Times.Once);
        }

        [Fact]
        [Trait("CRUD", "Admin")]
        public async Task TestAdminCanUpdateAnyone()
        {
            var authenticatedAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, false);
            var anotherAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, _mockBuilder.AdminUser, anotherAdmin });
            userListMock.Setup(u => u.Update(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // An Admin can update himself
            _mockBuilder.AdminUser.NewPassword = "NewPassword";
            var returnedUser = await _userService.Update(_mockBuilder.AdminUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.AdminUser);
            userListMock.Verify(u => u.Update(_mockBuilder.AdminUser), Times.Once);

            // An Admin can update an User
            _mockBuilder.UserUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(_mockBuilder.UserUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.UserUser);
            userListMock.Verify(u => u.Update(_mockBuilder.UserUser), Times.Once);

            // An Admin can update a Manager
            _mockBuilder.ManagerUser.NewPassword = "NewPassword";
            returnedUser = await _userService.Update(_mockBuilder.ManagerUser, authenticatedAdmin);
            returnedUser.ShouldBeSameAs(_mockBuilder.ManagerUser);
            userListMock.Verify(u => u.Update(_mockBuilder.ManagerUser), Times.Once);

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
            var authenticatedAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, false);
            var anotherAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(5, 
                new List<User> { _mockBuilder.ManagerUser, _mockBuilder.UserUser, _mockBuilder.AdminUser, anotherAdmin });
            userListMock.Setup(u => u.Remove(It.IsAny<User>()));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);

            // Admin can delete an User
            var returnedUser = await _userService.DeleteByUsername(_mockBuilder.UserUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(_mockBuilder.UserUser), Times.Once);

            // Admin can delete a Manager
            returnedUser = await _userService.DeleteByUsername(_mockBuilder.ManagerUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(_mockBuilder.ManagerUser), Times.Once);

            // Admin can delete himself
            returnedUser = await _userService.DeleteByUsername(_mockBuilder.AdminUser.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(_mockBuilder.AdminUser), Times.Once);

            // Admin can delete another Admin
            returnedUser = await _userService.DeleteByUsername(anotherAdmin.Username, authenticatedAdmin);
            userListMock.Verify(u => u.Remove(anotherAdmin), Times.Once);
        }
        #endregion
    }
}
