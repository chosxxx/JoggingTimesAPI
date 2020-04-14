using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Irony.Interpreter.Evaluator;
using Irony.Parsing;

namespace JoggingTimesAPI.Services
{
    public interface IUserService
    {
        Task<User> Authenticate(string userName, string password);
        Task<User> Create(User user);
        Task<User> Create(User user, User authenticatedUser);
        Task<User> Update(User user, User authenticatedUser);
        Task<User> GetByUsername(string userName, User authenticatedUser);
        Task<IList<User>> GetAll(string filter, IDictionary<string, bool> orderByFields, int rowsPerPage, int pageNumber);
        Task<User> DeleteByUsername(string userName, User authenticatedUser);
    }

    public class UserService : IUserService
    {
        private readonly JoggingTimesDataContext _dataContext;
        private IFilterEvaluator _filterEvaluator;
        
        public UserService(JoggingTimesDataContext context, IFilterEvaluator filterEvaluator)
        {
            _dataContext = context;
            _filterEvaluator = filterEvaluator;
        }

        public async Task<User> Authenticate(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                return null;

            var user = await _dataContext.Users.SingleOrDefaultAsync(
                u => u.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (user == null || !user.ValidatePassword(password))
            {
                return null;
            }

            return user;
        }

        public async Task<User> GetByUsername(string userName, User authenticatedUser)
        {
            if (string.IsNullOrEmpty(userName)) return null;

            var existingUser = await _dataContext.Users
                .SingleOrDefaultAsync(u => u.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

            AuthorizeAction(existingUser.Username, existingUser.Role, authenticatedUser);

            return existingUser;
        }

        public async Task<IList<User>> GetAll(string filter, IDictionary<string, bool> orderByFields, int rowsPerPage, int pageNumber)
        {
            var param = "Address";
            var propertyInfo = typeof(User).GetProperty(param);
            var orderByAddress = _dataContext.Users.OrderBy(x => propertyInfo.GetValue(x, null));

            var userQueryable = _dataContext.Users
                .Where(_filterEvaluator.EvaluateUserFilterPredicate(filter));

            if (orderByFields != null && orderByFields.Count >= 1)
            {
                IOrderedQueryable<User> orderedUserQueryable;
                var field = orderByFields.GetEnumerator();
                field.MoveNext();
                if (field.Current.Value)
                    orderedUserQueryable = userQueryable
                        .OrderBy((Expression<Func<User, object>>)_filterEvaluator.EvaluateUserKeySelector<User>(field.Current.Key));
                else
                    orderedUserQueryable = userQueryable
                        .OrderByDescending((Expression<Func<User, object>>)_filterEvaluator.EvaluateUserKeySelector<User>(field.Current.Key));

                while (field.MoveNext())
                {
                    if (field.Current.Value)
                        orderedUserQueryable = orderedUserQueryable
                            .ThenBy((Expression<Func<User, object>>)_filterEvaluator.EvaluateUserKeySelector<User>(field.Current.Key));
                    else
                        orderedUserQueryable = orderedUserQueryable
                            .ThenByDescending((Expression<Func<User, object>>)_filterEvaluator.EvaluateUserKeySelector<User>(field.Current.Key));
                }

                userQueryable = orderedUserQueryable;
            }

            userQueryable = userQueryable.Skip(rowsPerPage * (pageNumber - 1));
            userQueryable = userQueryable.Take(rowsPerPage);

            return await userQueryable.ToListAsync();
        }

        /// <summary>
        /// Create user with User role without being Authenticated.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<User> Create(User user)
        {
            user.Role = UserRole.User;
            return await Create(user, null);
        }

        public async Task<User> Create(User user, User authenticatedUser)
        {
            if (await _dataContext.Users.AnyAsync(u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"User {user.Username} already exists.");
            }

            // Anonymous user can only register as User
            if (user.Role != UserRole.User || authenticatedUser != null)
                AuthorizeAction(user.Username, user.Role, authenticatedUser);

            _dataContext.Users.Add(user);
            await _dataContext.SaveChangesAsync();

            return user;
        }

        public async Task<User> Update(User user, User authenticatedUser)
        {
            // Don't allow update TO a higher role than the authorized
            AuthorizeAction(user.Username, user.Role, authenticatedUser);

            var existingUser = await _dataContext.Users.SingleOrDefaultAsync(
                u => u.Username.Equals(user.Username, StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
                throw new ApplicationException($"User {user.Username} not found.");

            // AND don't allow update FROM a higher role than the authorized
            AuthorizeAction(existingUser.Username, existingUser.Role, authenticatedUser);

            if (!string.IsNullOrEmpty(user.EmailAddress))
                existingUser.EmailAddress = user.EmailAddress;

            if (!string.IsNullOrEmpty(user.NewPassword))
                existingUser.NewPassword = user.NewPassword;

            existingUser.Role = user.Role;

            _dataContext.Users.Update(existingUser);
            await _dataContext.SaveChangesAsync();

            return existingUser;
        }

        public async Task<User> DeleteByUsername(string userName, User authenticatedUser)
        {
            var existingUser = await _dataContext.Users.SingleOrDefaultAsync(
                u => u.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
                throw new ApplicationException($"User {userName} not found.");

            AuthorizeAction(existingUser.Username, existingUser.Role, authenticatedUser);

            _dataContext.Users.Remove(existingUser);
            await _dataContext.SaveChangesAsync();

            return existingUser;
        }

        private void AuthorizeAction(string targetUserName, UserRole targetRole, User authenticatedUser)
        {
            // An User with no higher priviledges can only CRUD himself.
            if ((authenticatedUser.Role == UserRole.User && (!authenticatedUser.Username.Equals(targetUserName) || targetRole > UserRole.User)) ||
            // A Manager can only CRUD Users and himself
                (authenticatedUser.Role == UserRole.Manager && targetRole != UserRole.User &&
                    (targetRole != UserRole.Manager || !authenticatedUser.Username.Equals(targetUserName))))
            // An Admin can CRUD any record
            {
                throw new InvalidOperationException("User is unauthorized to perform this action.");
            }
        }
    }
}
