using AutoMapper;
using Bogus;
using JoggingTimesAPI.Models;
using JoggingTimesAPI.Services;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

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
                Username = unique ? $"{user.Username}_Copy" : user.Username,
                Role = user.Role
            };
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

        public Mock<IUserService> GenerateMockUserService()
        {
            return new Mock<IUserService>();
        }

        public IMapper CreateMapper()
        {
            var mapper = new MapperConfiguration(cfg => {
                cfg.CreateMap<UserRegisterModel, User>();
                cfg.CreateMap<UserUpdateModel, User>();
            });

            return new Mapper(mapper);
        }
    }
}
