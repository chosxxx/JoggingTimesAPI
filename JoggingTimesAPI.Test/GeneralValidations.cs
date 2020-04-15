using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JoggingTimesAPI.Test
{
    public class GeneralValidations
    {
        public async Task AssertFullEnum<T>(IQueryable<T> q1, IQueryable<T> q2)
        {
            var l1 = await q1.ToListAsync();
            var l2 = await q2.ToListAsync();

            AssertFullEnum(l1, l2);
        }

        public void AssertFullEnum<T>(IList<T> l1, IList<T> l2)
        {
            l1.Count.ShouldBe(l2.Count);

            for (int i = 0; i < l1.Count; i++)
            {
                l1[i].ShouldBeSameAs(l2[i]);
            }
        }

        public void ValidateActionResult<T>(IActionResult actionResult, Type expectedResultType, IList<T> expectedList)
        {
            actionResult.ShouldBeOfType(expectedResultType);
            var requestResult = (List<T>)(actionResult as ObjectResult).Value;

            AssertFullEnum(requestResult, expectedList);
        }

        public void ValidateActionResult(IActionResult actionResult, Type expectedResultType, IDictionary<string, object> expectedPropertyValues)
        {
            actionResult.ShouldBeOfType(expectedResultType);
            var requestResult = (actionResult as ObjectResult).Value;

            foreach (var pv in expectedPropertyValues)
            {
                requestResult
                    .GetType()
                    .GetProperty(pv.Key)
                    .GetValue(requestResult)
                    .ShouldBe(pv.Value);
            }
        }
    }
}
