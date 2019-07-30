// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Incidents;
using NuGet.Services.Status;
using StatusAggregator.Parse;
using Xunit;
using Match = System.Text.RegularExpressions.Match;

namespace StatusAggregator.Tests.Parse
{
    public static class IncidentParsingHandlerTestUtility
    {
        public static EnvironmentRegexParsingFilter CreateEnvironmentFilter(params string[] environments)
        {
            var config = new StatusAggregatorConfiguration
            {
                Environments = environments
            };

            return new EnvironmentRegexParsingFilter(
                config,
                Mock.Of<ILogger<EnvironmentRegexParsingFilter>>());
        }

        public static SeverityRegexParsingFilter CreateSeverityFilter(int severity)
        {
            var config = new StatusAggregatorConfiguration
            {
                MaximumSeverity = severity
            };

            return new SeverityRegexParsingFilter(
                config,
                Mock.Of<ILogger<SeverityRegexParsingFilter>>());
        }

        public static void AssertTryParseAffectedComponentPath(
            IIncidentRegexParsingHandler handler, 
            Incident incident,
            bool success, 
            string expectedAffectedComponentPath = null)
        {
            var affectedComponentPath = string.Empty;
            var result =
                TryGetMatch(incident.Title, handler.RegexPattern, out var match) &&
                handler.TryParseAffectedComponentPath(incident, match.Groups, out affectedComponentPath);

            Assert.Equal(success, result);
            if (!result)
            {
                return;
            }

            Assert.Equal(expectedAffectedComponentPath, affectedComponentPath);
        }

        public static void AssertTryParseAffectedComponentStatus(
            IIncidentRegexParsingHandler handler,
            Incident incident,
            bool success,
            ComponentStatus expectedAffectedComponentStatus = ComponentStatus.Up)
        {
            var affectedComponentStatus = ComponentStatus.Up;
            var result =
                TryGetMatch(incident.Title, handler.RegexPattern, out var match) &&
                handler.TryParseAffectedComponentStatus(incident, match.Groups, out affectedComponentStatus);

            Assert.Equal(success, result);
            if (!result)
            {
                return;
            }

            Assert.Equal(expectedAffectedComponentStatus, affectedComponentStatus);
        }

        private static bool TryGetMatch(string title, string pattern, out Match match)
        {
            match = null;
            try
            {
                match = Regex.Match(title, pattern, RegexOptions.None, TimeSpan.FromSeconds(5));
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
