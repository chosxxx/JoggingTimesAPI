﻿using JoggingTimesAPI.Entities;
using JoggingTimesAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace JoggingTimesAPI.Test.Helpers
{
    public class FilterEvaluatorTest
    {
        private FilterEvaluator _evaluator;
        private GeneralValidations _validations;
        private MockBuilder _mockBuilder;

        public FilterEvaluatorTest()
        {
            _evaluator = new FilterEvaluator();
            _validations = new GeneralValidations();
            _mockBuilder = new MockBuilder();
        }

        [Fact]
        [Trait("Helpers", "FilterEvaluator")]
        public async Task TestFilterUserByRoleAndName()
        {
            var userList = _mockBuilder.GenerateMockUsers(100, new List<User>{ _mockBuilder.UserUser, _mockBuilder.AdminUser }).Object;

            var predicate = 
                _evaluator.EvaluateUserFilterPredicate($"(roLe Eq user or roLe == admin) and USERNAME ne \"{_mockBuilder.UserUser.Username}\"");
            var filteredByEvaluator = userList.Where(predicate);
            var filteredByLambda = userList.Where(
                u => (u.Role == UserRole.User || u.Role == UserRole.Admin) && !u.Username.Equals(_mockBuilder.UserUser.Username));

            await _validations.AssertFullEnum(filteredByEvaluator, filteredByLambda);
        }

        [Fact]
        [Trait("Helpers", "FilterEvaluator")]
        public async Task TestFilterLogByDate()
        {
            var userList = await _mockBuilder.GenerateMockUsers(5, new List<User> { _mockBuilder.UserUser }).Object.ToListAsync();
            var logList = _mockBuilder.GenerateMockLogs(200, userList).Object;
            var startDate = DateTime.Today.AddDays(-10);

            var predicate =
                _evaluator.EvaluateLogFilterPredicate($"StartDateTime gteq '{startDate:o}' AND StartDateTime eq UpdatedDateTime");
            var filteredByEvaluator = logList.Where(predicate);
            var filteredByLambda = logList.Where(
                l => l.StartDateTime >= startDate && l.StartDateTime == l.UpdatedDateTime);

            await _validations.AssertFullEnum(filteredByEvaluator, filteredByLambda);
        }
    }
}
