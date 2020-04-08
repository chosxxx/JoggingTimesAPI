using JoggingTimesAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Services
{
    public interface IUserService
    {
        Task<User> Authenticate(string userName, string password);
        Task<User> Register(User user);
        Task<User> Update(User user);
        Task<User> GetByUsername(string userName);
    }

    public class UserService : IUserService
    {
        private readonly DataContext _context;
        
        public UserService(DataContext context)
        {
            _context = context;
        }

        public async Task<User> Authenticate(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                return null;

            var user = await _context.Users.SingleAsync(u => u.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (user == null || !user.ValidatePassword(password))
            {
                return null;
            }

            return user;
        }

        public async Task<User> GetByUsername(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return null;

            return await _context.Users.SingleAsync(u => u.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<User> Register(User user)
        {
            if (await _context.Users.AnyAsync(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"User {user.Username} already exists.");
            }

            user.SetHashedPassword();

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User> Update(User user)
        {
            var existingUser = await _context.Users.SingleAsync(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
                throw new ApplicationException($"User {user.Username} not found.");

            if (!string.IsNullOrEmpty(user.EmailAddress))
                existingUser.EmailAddress = user.EmailAddress;

            if (!string.IsNullOrEmpty(user.NewPassword))
                existingUser.SetHashedPassword(user.NewPassword);

            existingUser.Role = user.Role;

            _context.Users.Update(existingUser);
            await _context.SaveChangesAsync();

            return existingUser;
        }
    }
}
