using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.Services;
using JoggingTimesAPI.WeatherProviders;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JoggingTimesAPI.Test.Services
{
    public class JoggingTimeLogServiceTest
    {
        private const string invalidUserErrorMessage = "User is unaothorized to perform this operation.";
        private const string inactiveLogErrorMessage = "Cannot update distance for inactive log.";
        private const string shorterDistanceErrorMessage = "New distance should be longer than current distance.";
        private const string logDoesntExistErrorMessage = "Could not find specified Log.";

        private MockBuilder _mockBuilder;
        private JoggingTimeLogService _logService;
        private Mock<JoggingTimesDataContext> _dataContext;

        public JoggingTimeLogServiceTest()
        {
            _mockBuilder = new MockBuilder();
            _dataContext = _mockBuilder.CreateDataContextMock();
            _logService = new JoggingTimeLogService(_dataContext.Object, null);
        }

        #region Start Log
        [Fact]
        [Trait("JoggingTimeLogCRUD", "StartLog")]
        public async Task TestValidUserCanStartJoggingTimeLog()
        {
            var invalidUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser });
            var logListMock = _mockBuilder.GenerateMockLogs(20, await userListMock.Object.ToListAsync(), new List<JoggingTimeLog>());

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // Invalid user cannot log
            (await Should
                .ThrowAsync<InvalidOperationException>(_logService.StartLog(invalidUser, 1, 2)))
                .Message.ShouldBe(invalidUserErrorMessage);

            // Valid user can log
            var returnedLog = await _logService.StartLog(_mockBuilder.UserUser, 1, 2);
            returnedLog.User.ShouldBeSameAs(_mockBuilder.UserUser);
            returnedLog.Latitude.ShouldBe(1);
            returnedLog.Longitude.ShouldBe(2);
            returnedLog.DistanceMetres.ShouldBe(0);
            returnedLog.Active.ShouldBeTrue();
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "StartLog")]
        public async Task TestStartLogShouldStopOpenOnes()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser });
            var userList = await userListMock.Object.ToListAsync();
            var activeLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
                {
                    l.Active = true;
                    l.User = _mockBuilder.UserUser;
                });
            var logListMock = _mockBuilder.GenerateMockLogs(20, userList, activeLog);

            logListMock.Setup(l => l.UpdateRange(It.IsAny<IEnumerable<JoggingTimeLog>>()))
                .Callback<IEnumerable<JoggingTimeLog>>(lList =>
                // If it's trying to disable activeLog, then we disable it here
                activeLog.Active = !lList.Any(l => l.JoggingTimeLogId == activeLog.JoggingTimeLogId && !l.Active));

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            var returnedLog = await _logService.StartLog(_mockBuilder.UserUser, 10, 20);
            returnedLog.User.ShouldBeSameAs(_mockBuilder.UserUser);
            returnedLog.Latitude.ShouldBe(10);
            returnedLog.Longitude.ShouldBe(20);
            returnedLog.DistanceMetres.ShouldBe(0);
            returnedLog.Active.ShouldBeTrue();
            activeLog.Active.ShouldBeFalse();
        }
        #endregion

        #region Update Distance
        [Fact]
        [Trait("JoggingTimeLogCRUD", "UpdateDistance")]
        public async Task TestUserCanUpdateDistanceForActiveLog()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser });
            var userList = await userListMock.Object.ToListAsync();
            var activeLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.UserUser;
            });
            var inactiveLogs = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = false;
                l.User = _mockBuilder.UserUser;
            }, 10);
            var logListMock = _mockBuilder.GenerateMockLogs(20, await userListMock.Object.ToListAsync(), new List<JoggingTimeLog>(inactiveLogs) { activeLog });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot update distance for inactive log
            var inactiveLog = inactiveLogs.First();
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.UpdateDistance(_mockBuilder.UserUser, inactiveLog.JoggingTimeLogId, inactiveLog.DistanceMetres + 10)))
                .Message.ShouldBe(inactiveLogErrorMessage);

            // User can update distance for active log
            var returnedLog = await _logService.UpdateDistance(_mockBuilder.UserUser, activeLog.JoggingTimeLogId, activeLog.DistanceMetres + 20);
            returnedLog.JoggingTimeLogId.ShouldBe(activeLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(activeLog.DistanceMetres);
            returnedLog.Active.ShouldBeTrue();
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "UpdateDistance")]
        public async Task TestUserCannotUpdateToShorterDistance()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser });
            var userList = await userListMock.Object.ToListAsync();
            var activeLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.UserUser;
            });
            var logListMock = _mockBuilder.GenerateMockLogs(20, userList, activeLog);

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot update to shorter distance
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.UpdateDistance(_mockBuilder.UserUser, activeLog.JoggingTimeLogId, activeLog.DistanceMetres - 10)))
                .Message.ShouldBe(shorterDistanceErrorMessage);

            // User can update to larger distance
            var returnedLog = await _logService.UpdateDistance(_mockBuilder.UserUser, activeLog.JoggingTimeLogId, activeLog.DistanceMetres + 20);
            returnedLog.JoggingTimeLogId.ShouldBe(activeLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(activeLog.DistanceMetres);
            returnedLog.Active.ShouldBeTrue();
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "UpdateDistance")]
        public async Task TestUserCanOnlyUpdateDistanceForHisLog()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser });
            var userList = await userListMock.Object.ToListAsync();
            var userLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.UserUser;
            });
            var managerLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.ManagerUser;
            });

            var logListMock = _mockBuilder.GenerateMockLogs(20, await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { userLog, managerLog });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot update log of other user
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.UpdateDistance(_mockBuilder.UserUser, managerLog.JoggingTimeLogId, managerLog.DistanceMetres + 10)))
                .Message.ShouldBe(invalidUserErrorMessage);

            // Manager cannot update log of other user
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.UpdateDistance(_mockBuilder.ManagerUser, userLog.JoggingTimeLogId, userLog.DistanceMetres + 20)))
                .Message.ShouldBe(invalidUserErrorMessage);

            // User can update his log
            var returnedLog = await _logService.UpdateDistance(_mockBuilder.UserUser, userLog.JoggingTimeLogId, userLog.DistanceMetres + 30);
            returnedLog.JoggingTimeLogId.ShouldBe(userLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(userLog.DistanceMetres);
            returnedLog.Active.ShouldBeTrue();

            // Manager can update his log
            returnedLog = await _logService.UpdateDistance(_mockBuilder.ManagerUser, managerLog.JoggingTimeLogId, managerLog.DistanceMetres + 40);
            returnedLog.JoggingTimeLogId.ShouldBe(managerLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(managerLog.DistanceMetres);
            returnedLog.Active.ShouldBeTrue();
        }
        #endregion

        #region Stop Log
        [Fact]
        [Trait("JoggingTimeLogCRUD", "StopLog")]
        public async Task TestUserCanOnlyStopValidLogId()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser });
            var userList = await userListMock.Object.ToListAsync();
            var testLogs = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.UserUser;
            }, 2);
            var invalidLog = testLogs.First();
            var validLog = testLogs.Single(l => l.JoggingTimeLogId != invalidLog.JoggingTimeLogId);

            var logListMock = _mockBuilder.GenerateMockLogs(20, await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { validLog });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot stop invalid log
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.StopLog(_mockBuilder.UserUser, invalidLog.JoggingTimeLogId, invalidLog.DistanceMetres + 10)))
                .Message.ShouldBe(logDoesntExistErrorMessage);

            // User can stop valid log
            var returnedLog = await _logService.StopLog(_mockBuilder.UserUser, validLog.JoggingTimeLogId, validLog.DistanceMetres + 20);
            returnedLog.JoggingTimeLogId.ShouldBe(validLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(validLog.DistanceMetres);
            returnedLog.Active.ShouldBeFalse();
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "StopLog")]
        public async Task TestUserCanOnlyStopHisLog()
        {
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser });
            var userList = await userListMock.Object.ToListAsync();
            var userLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.UserUser;
            });
            var managerLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) =>
            {
                l.Active = true;
                l.User = _mockBuilder.ManagerUser;
            });

            var logListMock = _mockBuilder.GenerateMockLogs(20, await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { userLog, managerLog });

            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot stop log of other user
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.StopLog(_mockBuilder.UserUser, managerLog.JoggingTimeLogId, managerLog.DistanceMetres + 10)))
                .Message.ShouldBe(invalidUserErrorMessage);

            // Manager cannot stop log of other user
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.StopLog(_mockBuilder.ManagerUser, userLog.JoggingTimeLogId, userLog.DistanceMetres + 20)))
                .Message.ShouldBe(invalidUserErrorMessage);

            // User can stop his log
            var returnedLog = await _logService.StopLog(_mockBuilder.UserUser, userLog.JoggingTimeLogId, userLog.DistanceMetres + 20);
            returnedLog.JoggingTimeLogId.ShouldBe(userLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(userLog.DistanceMetres);
            returnedLog.Active.ShouldBeFalse();

            // Manager can stop his log
            returnedLog = await _logService.StopLog(_mockBuilder.ManagerUser, managerLog.JoggingTimeLogId, managerLog.DistanceMetres + 40);
            returnedLog.JoggingTimeLogId.ShouldBe(managerLog.JoggingTimeLogId);
            returnedLog.DistanceMetres.ShouldBe(managerLog.DistanceMetres);
            returnedLog.Active.ShouldBeFalse();
        }
        #endregion

        #region Delege Log
        [Fact]
        [Trait("JoggingTimeLogCRUD", "DeleteLog")]
        public async Task TestUserCanOnlyDeleteHisOwnLogs()
        {
            var authenticatedUser = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.UserUser, true);
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, authenticatedUser });
            var userList = await userListMock.Object.ToListAsync();
            var userLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.UserUser);
            var authenticatedUserLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = authenticatedUser);

            var logListMock = _mockBuilder.GenerateMockLogs(20, 
                await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { authenticatedUserLog, userLog });

            logListMock.Setup(l => l.Remove(It.IsAny<JoggingTimeLog>()));
            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // User cannot delete log of other user
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.DeleteLog(authenticatedUser, userLog.JoggingTimeLogId)))
                .Message.ShouldBe(invalidUserErrorMessage);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Never);

            // User can delete his log
            var returnedLog = await _logService.DeleteLog(authenticatedUser, authenticatedUserLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(authenticatedUserLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Once);
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "DeleteLog")]
        public async Task TestManagerCanDeleteUserAndHisOwnLogs()
        {
            var authenticatedManager = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.ManagerUser, true);
            
            var userListMock = _mockBuilder.GenerateMockUsers(10, new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser, authenticatedManager });
            var userList = await userListMock.Object.ToListAsync();
            var authenticatedManagerLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = authenticatedManager);
            var anotherManagerLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.ManagerUser);
            var userLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.UserUser);

            var logListMock = _mockBuilder.GenerateMockLogs(20,
                await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { userLog, authenticatedManagerLog, anotherManagerLog });

            logListMock.Setup(l => l.Remove(It.IsAny<JoggingTimeLog>()));
            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // Manager cannot delete log of other manager
            (await Should
                .ThrowAsync<InvalidOperationException>(
                    _logService.DeleteLog(authenticatedManager, anotherManagerLog.JoggingTimeLogId)))
                .Message.ShouldBe(invalidUserErrorMessage);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Never);

            // Manager can delete user log
            var returnedLog = await _logService.DeleteLog(authenticatedManager, userLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(userLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Once);

            // Manager can delete his log
            returnedLog = await _logService.DeleteLog(authenticatedManager, authenticatedManagerLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(authenticatedManagerLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Exactly(2));
        }

        [Fact]
        [Trait("JoggingTimeLogCRUD", "DeleteLog")]
        public async Task TestAdminCanDeleteAnyLog()
        {
            var authenticatedAdmin = _mockBuilder.SimpleCopyUserFrom(_mockBuilder.AdminUser, true);

            var userListMock = _mockBuilder.GenerateMockUsers(10, 
                new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser, _mockBuilder.AdminUser, authenticatedAdmin });
            var userList = await userListMock.Object.ToListAsync();
            var authenticatedAdminLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = authenticatedAdmin);
            var anotherAdminLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.AdminUser);
            var userLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.UserUser);
            var managerLog = _mockBuilder.GenerateMockLogsWithRules(userList, (f, l) => l.User = _mockBuilder.ManagerUser);

            var logListMock = _mockBuilder.GenerateMockLogs(20,
                await userListMock.Object.ToListAsync(), new List<JoggingTimeLog> { userLog, managerLog, authenticatedAdminLog, anotherAdminLog });

            logListMock.Setup(l => l.Remove(It.IsAny<JoggingTimeLog>()));
            _dataContext.Setup(x => x.Users).Returns(userListMock.Object);
            _dataContext.Setup(x => x.JoggingTimeLogs).Returns(logListMock.Object);

            // Admin can delete user log
            var returnedLog = await _logService.DeleteLog(authenticatedAdmin, userLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(userLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Once);

            // Admin can delete manager log
            returnedLog = await _logService.DeleteLog(authenticatedAdmin, managerLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(managerLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Exactly(2));

            // Admin can delete another admin log
            returnedLog = await _logService.DeleteLog(authenticatedAdmin, anotherAdminLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(anotherAdminLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Exactly(3));

            // Admin can delete his log
            returnedLog = await _logService.DeleteLog(authenticatedAdmin, authenticatedAdminLog.JoggingTimeLogId);
            returnedLog.JoggingTimeLogId.ShouldBe(authenticatedAdminLog.JoggingTimeLogId);
            logListMock.Verify(l => l.Remove(It.IsAny<JoggingTimeLog>()), Times.Exactly(4));
        }
        #endregion
    }
}
