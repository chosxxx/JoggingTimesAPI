using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using JoggingTimesAPI.WeatherProviders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Services
{
    public interface IJoggingTimeLogService
    {
        Task<IList<JoggingTimeLog>> GetAll(string filter, IDictionary<string, bool> orderByFields, int rowsPerPage, int pageNumber);
        Task<JoggingTimeLog> StartLog(User authenticatedUser, double latitude, double longitude);
        Task<JoggingTimeLog> UpdateDistance(User authenticatedUser, int logId, double distance);
        Task<JoggingTimeLog> StopLog(User authenticatedUser, int logId, double finalDistance);
        Task<JoggingTimeLog> DeleteLog(User authenticatedUser, int logId);
    }

    public class JoggingTimeLogService : IJoggingTimeLogService
    {
        private const string invalidUserErrorMessage = "User is unaothorized to perform this operation.";
        private const string inactiveLogErrorMessage = "Cannot update distance for inactive log.";
        private const string shorterDistanceErrorMessage = "New distance should be longer than current distance.";
        private const string logDoesntExistErrorMessage = "Could not find specified Log.";

        private readonly JoggingTimesDataContext _dataContext;
        private IFilterEvaluator _filterEvaluator;

        public JoggingTimeLogService(JoggingTimesDataContext context, IFilterEvaluator filterEvaluator)
        {
            _dataContext = context;
            _filterEvaluator = filterEvaluator;
        }

        public async Task<IList<JoggingTimeLog>> GetAll(string filter, IDictionary<string, bool> orderByFields, int rowsPerPage, int pageNumber)
        {
            var logQueryable = _dataContext.JoggingTimeLogs
                .Where(_filterEvaluator.EvaluateLogFilterPredicate(filter));

            if (orderByFields != null && orderByFields.Count >= 1)
            {
                IOrderedQueryable<JoggingTimeLog> orderedUserQueryable;
                var field = orderByFields.GetEnumerator();
                field.MoveNext();
                if (field.Current.Value)
                    orderedUserQueryable = 
                        logQueryable.OrderBy((Expression<Func<JoggingTimeLog, object>>)
                        _filterEvaluator.EvaluateLogKeySelector<JoggingTimeLog>(field.Current.Key));
                else
                    orderedUserQueryable = 
                        logQueryable.OrderByDescending((Expression<Func<JoggingTimeLog, object>>)
                        _filterEvaluator.EvaluateLogKeySelector<JoggingTimeLog>(field.Current.Key));

                while (field.MoveNext())
                {
                    if (field.Current.Value)
                        orderedUserQueryable = 
                            orderedUserQueryable.ThenBy((Expression<Func<JoggingTimeLog, object>>)
                            _filterEvaluator.EvaluateLogKeySelector<JoggingTimeLog>(field.Current.Key));
                    else
                        orderedUserQueryable = 
                            orderedUserQueryable.ThenByDescending((Expression<Func<JoggingTimeLog, object>>)
                            _filterEvaluator.EvaluateLogKeySelector<JoggingTimeLog>(field.Current.Key));
                }

                logQueryable = orderedUserQueryable;
            }

            logQueryable = logQueryable.Skip(rowsPerPage * (pageNumber - 1));
            logQueryable = logQueryable.Take(rowsPerPage);

            return await logQueryable.ToListAsync();
        }

        public async Task<JoggingTimeLog> DeleteLog(User authenticatedUser, int logId)
        {
            JoggingTimeLog log = await ValidateLogAndUser(authenticatedUser, logId, false, true);

            _dataContext.JoggingTimeLogs.Remove(log);
            await _dataContext.SaveChangesAsync();

            return log;
        }

        public async Task<JoggingTimeLog> StopLog(User authenticatedUser, int logId, double finalDistance)
        {
            JoggingTimeLog log = await ValidateLogAndUser(authenticatedUser, logId, true, false);

            if (finalDistance < log.DistanceMetres)
                throw new InvalidOperationException(shorterDistanceErrorMessage);

            log.DistanceMetres = finalDistance;
            log.UpdatedDateTime = DateTime.UtcNow;
            log.Active = false;

            _dataContext.Update(log);
            await _dataContext.SaveChangesAsync();

            return log;
        }

        public async Task<JoggingTimeLog> StartLog(User authenticatedUser, double latitude, double longitude)
        {
            if (authenticatedUser == null || !await _dataContext.Users.AnyAsync(u => u.Username.Equals(authenticatedUser.Username)))
            {
                throw new InvalidOperationException(invalidUserErrorMessage);
            }

            var activeLogs = _dataContext.JoggingTimeLogs
                .Where(j => j.User.Username.Equals(authenticatedUser.Username) && j.Active);
            await activeLogs.ForEachAsync(l => l.Active = false);
            _dataContext.UpdateRange(activeLogs);

            var log = new JoggingTimeLog
            {
                User = authenticatedUser,
                UserName = authenticatedUser.Username,
                StartDateTime = DateTime.UtcNow,
                UpdatedDateTime = DateTime.UtcNow,
                DistanceMetres = 0,
                Latitude = latitude,
                Longitude = longitude,
                Active = true
            };

            _dataContext.JoggingTimeLogs.Add(log);
            await _dataContext.SaveChangesAsync();

            return log;
        }

        public async Task<JoggingTimeLog> UpdateDistance(User authenticatedUser, int logId, double distance)
        {
            JoggingTimeLog log = await ValidateLogAndUser(authenticatedUser, logId, true, false);

            if (distance < log.DistanceMetres)
                throw new InvalidOperationException(shorterDistanceErrorMessage);

            log.DistanceMetres = distance;
            log.UpdatedDateTime = DateTime.UtcNow;

            _dataContext.Update(log);
            await _dataContext.SaveChangesAsync();

            return log;
        }

        private async Task<JoggingTimeLog> ValidateLogAndUser(User authenticatedUser, int logId, bool validateActive, bool validateRole)
        {
            var log = await _dataContext.JoggingTimeLogs.SingleOrDefaultAsync(l => l.JoggingTimeLogId == logId);
            if (log == null)
                throw new InvalidOperationException(logDoesntExistErrorMessage);

            if (authenticatedUser == null)
                throw new InvalidOperationException(invalidUserErrorMessage);

            if (validateRole) 
                AuthorizeAction(log.UserName, log.User.Role, authenticatedUser);
            else
            if (!log.UserName.Equals(authenticatedUser.Username))
                throw new InvalidOperationException(invalidUserErrorMessage);

            if (validateActive && !log.Active)
                throw new InvalidOperationException(inactiveLogErrorMessage);

            return log;
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
                throw new InvalidOperationException(invalidUserErrorMessage);
            }
        }
    }
}
