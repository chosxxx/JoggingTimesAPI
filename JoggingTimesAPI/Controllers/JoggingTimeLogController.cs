using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using JoggingTimesAPI.Models;
using JoggingTimesAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AutoMapper;
using JoggingTimesAPI.Helpers;
using Microsoft.Extensions.Options;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.WeatherProviders;
using Newtonsoft.Json.Linq;

namespace JoggingTimesAPI.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class JoggingTimeLogController : ControllerBase
    {
        private IJoggingTimeLogService _logService;
        private User _authenticatedUser;
        private readonly IWeatherProvider _weatherProvider;

        public User AuthenticatedUser
        {
            get
            {
                if (User != null && _authenticatedUser == null)
                {
                    _authenticatedUser = new User
                    {
                        Username = User.Identity.Name,
                        Role = Enum.Parse<UserRole>(User.Claims.Single(c => c.Type == ClaimTypes.Role).Value)
                    };
                }
                return _authenticatedUser;
            }
            set
            {
                if (User != null)
                    throw new ApplicationException("Cannot change Authenticated User, property is read-only.");
                _authenticatedUser = value;
            }
        }

        public JoggingTimeLogController(
            IJoggingTimeLogService logService,
            IMapper mapper,
            IWeatherProvider weatherProvider,
            IOptions<AppSettings> appSettings)
        {
            _logService = logService;
            _weatherProvider = weatherProvider;
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetAll([FromBody]GetAllModel model)
        {
            try
            {
                var logList = await _logService.GetAll(model.Filter, model.RowsPerPage, model.PageNumber, AuthenticatedUser);
                return Ok(logList);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("start")]
        public async Task<IActionResult> Start([FromBody]JoggingTimeLogStartModel model)
        {
            JoggingTimeLog log;
            try
            {
                log = await _logService.StartLog(AuthenticatedUser, model.Latitude, model.Longitude);
            }
            catch(Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            JObject currentWeather;
            try
            {
                currentWeather = await _weatherProvider.GetCurrentWeather(model.Latitude, model.Longitude);
            }
            catch (Exception ex)
            {
                currentWeather = JObject.FromObject(new { message = $"Unable to get weather info. Error: {ex.Message}" });
            }

            return Ok(new
            {
                log.JoggingTimeLogId,
                CurrentWeather = currentWeather
            });
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateDistance([FromBody]JoggingTimeLogUpdateModel model)
        {
            try
            {
                var log = await _logService.UpdateDistance(AuthenticatedUser, model.JoggingTimeLogId, model.DistanceMetres);

                return Ok(log);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("stop")]
        public async Task<IActionResult> Stop([FromBody]JoggingTimeLogUpdateModel model)
        {
            try
            {
                var log = await _logService.StopLog(AuthenticatedUser, model.JoggingTimeLogId, model.DistanceMetres);

                return Ok(log);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteById([FromRoute]int id)
        {
            try
            {
                var log = await _logService.DeleteLog(AuthenticatedUser, id);

                return Ok(log);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
