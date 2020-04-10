using Bogus;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JoggingTimesAPI.Test
{
    public class MockBuilder
    {
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
    }
}
