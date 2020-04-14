using JoggingTimesAPI.Controllers;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.Services;
using JoggingTimesAPI.WeatherProviders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace JoggingTimesAPI.Test.Controllers
{
    public class JoggingTimeLogControllerTest
    {
        private MockBuilder _mockBuilder;
        private Mock<IJoggingTimeLogService> _logService;
        private Mock<IWeatherProvider> _weatherProvider;
        private JoggingTimeLogController _logController;

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

        public JoggingTimeLogControllerTest()
        {
            _mockBuilder = new MockBuilder();
            _logService = _mockBuilder.GenerateMockLogService();
            _weatherProvider = _mockBuilder.GenerateMockWeatherProvider();
            _logController = new JoggingTimeLogController(
                _logService.Object,
                _mockBuilder.CreateMapper(),
                _weatherProvider.Object,
                Options.Create(new AppSettings { Secret = "DontUseThisUnsafeSecret" })
            );
        }

        [Fact]
        [Trait("Actions", "JoggingTimeLog")]
        public async Task TestStartOnErrorShouldReturnBadRequest()
        {
            var someLog = _mockBuilder.GenerateMockLogsWithRules(new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser },
                (f, l) => 
                {
                    l.User = _mockBuilder.UserUser;
                    l.Active = true;
                });
            var currentWeather = _mockBuilder.GenerateCurrentWeatherInfo();
            _logService.Setup(l => l.StartLog(null, It.IsAny<double>(), It.IsAny<double>()))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _logService.Setup(l => l.StartLog(It.IsNotNull<User>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(someLog);
            _weatherProvider.Setup(w => w.GetCurrentWeather(It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(currentWeather);

            // On error, should return bad request
            var actionResult = await _logController.Start(
                new Models.JoggingTimeLogStartModel { Latitude = someLog.Latitude, Longitude = someLog.Longitude });
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return log data
            _logController.AuthenticatedUser = _mockBuilder.UserUser;
            actionResult = await _logController.Start(
                new Models.JoggingTimeLogStartModel { Latitude = someLog.Latitude, Longitude = someLog.Longitude });
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "JoggingTimeLogId", someLog.JoggingTimeLogId },
                    { "CurrentWeather", currentWeather }
                });
            _logController.AuthenticatedUser = null;

            _weatherProvider.Verify(w => w.GetCurrentWeather(It.IsAny<double>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        [Trait("Actions", "JoggingTimeLog")]
        public async Task TestStartOnWeatherFailsShouldReturnOkAndErrorFromAPI()
        {
            const string errorFromWeatherAPI = "Dummy message from Weather API.";
            var someLog = _mockBuilder.GenerateMockLogsWithRules(new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser },
                (f, l) =>
                {
                    l.User = _mockBuilder.UserUser;
                    l.Active = true;
                });
            _logService.Setup(l => l.StartLog(It.IsNotNull<User>(), It.IsAny<double>(), It.IsAny<double>()))
                .ReturnsAsync(someLog);
            _weatherProvider.Setup(w => w.GetCurrentWeather(It.IsAny<double>(), It.IsAny<double>()))
                .ThrowsAsync(new Exception(errorFromWeatherAPI));

            // If Weather API fails, it should return Ok with Log ID and error from API
            _logController.AuthenticatedUser = _mockBuilder.UserUser;
            var actionResult = await _logController.Start(
                new Models.JoggingTimeLogStartModel { Latitude = someLog.Latitude, Longitude = someLog.Longitude });
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "JoggingTimeLogId", someLog.JoggingTimeLogId },
                    { "CurrentWeather", JObject.FromObject( new { message = $"Unable to get weather info. Error: {errorFromWeatherAPI}" }) }
                });
            _logController.AuthenticatedUser = null;

            _weatherProvider.Verify(w => w.GetCurrentWeather(It.IsAny<double>(), It.IsAny<double>()), Times.Once);
        }

        [Fact]
        [Trait("Actions", "JoggingTimeLog")]
        public async Task TestUpdateOnErrorShouldReturnBadRequest()
        {
            var someLog = _mockBuilder.GenerateMockLogsWithRules(new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser },
                (f, l) =>
                {
                    l.User = _mockBuilder.UserUser;
                    l.Active = true;
                });
            _logService.Setup(l => l.UpdateDistance(null, It.IsAny<int>(), It.IsAny<double>()))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _logService.Setup(l => l.UpdateDistance(It.IsNotNull<User>(), It.IsAny<int>(), It.IsAny<double>()))
                .ReturnsAsync(someLog);

            // On error, should return bad request
            var actionResult = await _logController.UpdateDistance(
                new Models.JoggingTimeLogUpdateModel { JoggingTimeLogId = someLog.JoggingTimeLogId, DistanceMetres = someLog.DistanceMetres + 10 });
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return log data
            _logController.AuthenticatedUser = _mockBuilder.UserUser;
            actionResult = await _logController.UpdateDistance(
                new Models.JoggingTimeLogUpdateModel { JoggingTimeLogId = someLog.JoggingTimeLogId, DistanceMetres = someLog.DistanceMetres + 10 });
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "JoggingTimeLogId", someLog.JoggingTimeLogId }
                });
            _logController.AuthenticatedUser = null;
        }

        [Fact]
        [Trait("Actions", "JoggingTimeLog")]
        public async Task TestStopOnErrorShouldReturnBadRequest()
        {
            var someLog = _mockBuilder.GenerateMockLogsWithRules(new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser },
                (f, l) =>
                {
                    l.User = _mockBuilder.UserUser;
                    l.Active = true;
                });
            _logService.Setup(l => l.StopLog(null, It.IsAny<int>(), It.IsAny<double>()))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _logService.Setup(l => l.StopLog(It.IsNotNull<User>(), It.IsAny<int>(), It.IsAny<double>()))
                .ReturnsAsync(someLog);

            // On error, should return bad request
            var actionResult = await _logController.Stop(
                new Models.JoggingTimeLogUpdateModel { JoggingTimeLogId = someLog.JoggingTimeLogId, DistanceMetres = someLog.DistanceMetres + 10 });
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return log data
            _logController.AuthenticatedUser = _mockBuilder.UserUser;
            actionResult = await _logController.Stop(
                new Models.JoggingTimeLogUpdateModel { JoggingTimeLogId = someLog.JoggingTimeLogId, DistanceMetres = someLog.DistanceMetres + 10 });
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "JoggingTimeLogId", someLog.JoggingTimeLogId }
                });
            _logController.AuthenticatedUser = null;
        }

        [Fact]
        [Trait("Actions", "JoggingTimeLog")]
        public async Task TestDeleteOnErrorShouldReturnBadRequest()
        {
            var someLog = _mockBuilder.GenerateMockLogsWithRules(new List<User> { _mockBuilder.UserUser, _mockBuilder.ManagerUser },
                (f, l) =>
                {
                    l.User = _mockBuilder.UserUser;
                    l.Active = true;
                });
            _logService.Setup(l => l.DeleteLog(null, It.IsAny<int>()))
                .ThrowsAsync(new InvalidOperationException("Exception message"));
            _logService.Setup(l => l.DeleteLog(It.IsNotNull<User>(), It.IsAny<int>()))
                .ReturnsAsync(someLog);

            // On error, should return bad request
            var actionResult = await _logController.DeleteById(someLog.JoggingTimeLogId);
            ValidateActionResult(actionResult, typeof(BadRequestObjectResult),
                new Dictionary<string, object> {
                    { "message", "Exception message" }
                });

            // If Ok, should return log data
            _logController.AuthenticatedUser = _mockBuilder.UserUser;
            actionResult = await _logController.DeleteById(someLog.JoggingTimeLogId);
            ValidateActionResult(actionResult, typeof(OkObjectResult),
                new Dictionary<string, object> {
                    { "JoggingTimeLogId", someLog.JoggingTimeLogId }
                });
            _logController.AuthenticatedUser = null;
        }
    }
}
