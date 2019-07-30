// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Incidents;
using Xunit;

namespace StatusAggregator.Tests.Parse
{
    public class SeverityRegexParsingFilterTests
    {
        public static IEnumerable<object[]> ReturnsTrueWhenSeverityLessThanOrEqualTo_Data
        {
            get
            {
                foreach (var max in PossibleSeverities)
                {
                    foreach (var input in PossibleSeverities)
                    {
                        yield return new object[] { max, input };
                    }
                }
            }
        }

        private static readonly IEnumerable<int> PossibleSeverities = new[] { 1, 2, 3, 4 };

        [Theory]
        [MemberData(nameof(ReturnsTrueWhenSeverityLessThanOrEqualTo_Data))]
        public void ReturnsTrueWhenSeverityLessThanOrEqualTo(int maximumSeverity, int inputSeverity)
        {
            var incident = new Incident
            {
                Severity = inputSeverity
            };

            var filter = IncidentParsingHandlerTestUtility.CreateSeverityFilter(maximumSeverity);

            var result = filter.ShouldParse(incident, null);

            Assert.Equal(inputSeverity <= maximumSeverity, result);
        }
    }
}
