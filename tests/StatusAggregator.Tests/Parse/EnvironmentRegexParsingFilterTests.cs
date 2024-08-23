// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using NuGet.Services.Incidents;
using StatusAggregator.Parse;
using Xunit;
using Match = System.Text.RegularExpressions.Match;

namespace StatusAggregator.Tests.Parse
{
    public class EnvironmentRegexParsingFilterTests
    {
        public class TheShouldParseMethod
            : EnvironmentRegexParsingFilterTest
        {
            [Fact]
            public void ReturnsTrueWithoutEnvironmentGroup()
            {
                var match = Match.Empty;

                var result = Filter.ShouldParse(Incident, match.Groups);

                Assert.True(result);
            }

            [Fact]
            public void ReturnsFalseIfIncorrectEnvironment()
            {
                var match = GetMatchWithEnvironmentGroup("imaginary");

                var result = Filter.ShouldParse(Incident, match.Groups);

                Assert.False(result);
            }

            [Theory]
            [InlineData(Environment1)]
            [InlineData(Environment2)]
            public void ReturnsTrueIfCorrectEnvironment(string environment)
            {
                var match = GetMatchWithEnvironmentGroup(environment);

                var result = Filter.ShouldParse(Incident, match.Groups);

                Assert.True(result);
            }

            private static Match GetMatchWithEnvironmentGroup(string environment)
            {
                return Regex.Match($"[{environment}]", $@"\[(?<{ EnvironmentRegexParsingFilter.EnvironmentGroupName}>.*)\]");
            }
        }

        public class EnvironmentRegexParsingFilterTest
        {
            public const string Environment1 = "env1";
            public const string Environment2 = "env2";

            public static Incident Incident = new Incident();

            public EnvironmentRegexParsingFilter Filter { get; }

            public EnvironmentRegexParsingFilterTest()
            {
                Filter = IncidentParsingHandlerTestUtility.CreateEnvironmentFilter(Environment1, Environment2);
            }
        }
    }
}
