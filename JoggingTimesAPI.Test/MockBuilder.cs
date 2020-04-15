using AutoMapper;
using Bogus;
using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.Models;
using JoggingTimesAPI.Services;
using JoggingTimesAPI.WeatherProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;
using Newtonsoft.Json.Linq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Test
{
    public class MockBuilder
    {
        #region Users to validate
        private const string NotASafePW = "NotASafePW";

        private User _userUser;
        private User _managerUser;
        private User _adminUser;

        public User UserUser
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

        public User ManagerUser
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

        public User AdminUser
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
        
        /// <summary>
        /// Creates a copy of the user only with the basic information
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public User SimpleCopyUserFrom(User user, bool unique)
        {
            return new User
            {
                Username = unique ? $"{user.Username}_{(new Faker()).IndexGlobal}" : user.Username,
                Role = user.Role
            };
        }

        public JObject GenerateCurrentWeatherInfo()
        {
            return JObject.FromObject(new
            {
                lat = 20.6765814,
                lon = -103.3810662,
                temp = new
                {
                    value = 21,
                    units = "C"
                },
                feels_like = new
                {
                    value = 21,
                    units = "C"
                },
                wind_speed = new
                {
                    value = 2,
                    units = "m/s"
                },
                humidity = new
                {
                    value = 25,
                    units = "%"
                },
                precipitation = new
                {
                    value = 0,
                    units = "mm/hr"
                },
                cloud_cover = new
                {
                    value = 38,
                    units = "%"
                },
                observation_time = new
                {
                    value = DateTime.UtcNow.ToString("o")
                }
            });
        }

        public Mock<DbSet<User>> GenerateMockUsers(int count, IList<User> include = null)
        {
            var userList = new Faker<User>()
                .CustomInstantiator(f => new User())
                .RuleFor(u => u.Username, f => f.Internet.UserName())
                .RuleFor(u => u.NewPassword, f => f.Internet.Password())
                .RuleFor(u => u.EmailAddress, f => f.Internet.Email())
                .RuleFor(u => u.Role, f => f.PickRandom((UserRole[])Enum.GetValues(typeof(UserRole))))
                .Generate(count);

            if (include != null) userList.AddRange(include);

            return userList.AsQueryable().BuildMockDbSet();
        }

        public JoggingTimeLog GenerateMockLogsWithRules(IList<User> availableUsers,
            Action<Faker, JoggingTimeLog> rulesToInclude)
        {
            return GenerateMockLogsWithRules(availableUsers, rulesToInclude, 1).Single();
        }

        public IList<JoggingTimeLog> GenerateMockLogsWithRules(IList<User> availableUsers, 
            Action<Faker, JoggingTimeLog> rulesToInclude, int countForRules)
        {
            var logList = new Faker<JoggingTimeLog>()
                .RuleFor(l => l.JoggingTimeLogId, f => f.IndexGlobal)
                .RuleFor(l => l.User, f => f.PickRandom(availableUsers))
                .RuleFor(l => l.DistanceMetres, f => f.Random.Double(0, 50000))
                .RuleFor(l => l.Latitude, f => f.Random.Double(-90, 90))
                .RuleFor(l => l.Longitude, f => f.Random.Double(-180, 180))
                .RuleFor(l => l.StartDateTime, f => f.Date.Recent(days: 30))
                .RuleFor(l => l.Active, f => f.Random.Bool())
                .Rules(rulesToInclude)
                .FinishWith((f, l) =>
                {
                    l.UpdatedDateTime = l.StartDateTime.AddSeconds(
                        // 30% of the logs will have same UpdatedDateTime as StartDateTime
                        f.Random.Bool(0.3f) ? 0 : f.Random.UInt(max: 3 * 60 * 60));
                    l.Username = l.User.Username;
                })
                .Generate(countForRules);

            return logList;
        }

        public Mock<DbSet<JoggingTimeLog>> GenerateMockLogs(int count, IList<User> availableUsers, JoggingTimeLog include)
        {
            return GenerateMockLogs(count, availableUsers, new List<JoggingTimeLog> { include });
        }

        public Mock<DbSet<JoggingTimeLog>> GenerateMockLogs(int count, IList<User> availableUsers, IList<JoggingTimeLog> include = null)
        {
            var logList = new Faker<JoggingTimeLog>()
                .CustomInstantiator(f => new JoggingTimeLog())
                .RuleFor(l => l.JoggingTimeLogId, f => f.IndexGlobal)
                .RuleFor(l => l.User, f => f.PickRandom(availableUsers))
                .RuleFor(l => l.DistanceMetres, f => f.Random.Double(0, 50000))
                .RuleFor(l => l.Latitude, f => f.Random.Double(-90, 90))
                .RuleFor(l => l.Longitude, f => f.Random.Double(-180, 180))
                .RuleFor(l => l.StartDateTime, f => f.Date.Recent(days: 30))
                .RuleFor(l => l.Active, f => f.Random.Bool())
                .FinishWith((f, l) =>
                {
                    l.UpdatedDateTime = l.StartDateTime.AddSeconds(
                        // 30% of the logs will have same UpdatedDateTime as StartDateTime
                        f.Random.Bool(0.3f) ? 0 : f.Random.UInt(max: 3 * 60 * 60));
                    l.Username = l.User.Username;
                })
                .Generate(count);

            if (include != null) logList.AddRange(include);

            return logList.AsQueryable().BuildMockDbSet();
        }

        public Mock<IUserService> GenerateMockUserService()
        {
            return new Mock<IUserService>();
        }


        public Mock<IJoggingTimeLogService> GenerateMockLogService()
        {
            return new Mock<IJoggingTimeLogService>();
        }

        public IMapper CreateMapper()
        {
            var mapper = new MapperConfiguration(cfg => {
                cfg.CreateMap<UserRegisterModel, User>();
                cfg.CreateMap<UserUpdateModel, User>();
            });

            return new Mapper(mapper);
        }

        public Mock<IWeatherProvider> GenerateMockWeatherProvider()
        {
            return new Mock<IWeatherProvider>();
        }

        public Mock<JoggingTimesDataContext> CreateDataContextMock()
        {

            return new Mock<JoggingTimesDataContext>(
                new DbContextOptionsBuilder<JoggingTimesDataContext>()
                .UseInMemoryDatabase("TestJogglingTimesDB").Options);
        }
    }
}
