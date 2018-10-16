// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moq;
using StatusAggregator.Parse;
using Match = System.Text.RegularExpressions.Match;

namespace StatusAggregator.Tests.Parse
{
    public static class ParsingUtility
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

        public static Match GetMatchWithGroup(string group, string value)
        {
            return GetMatchWithGroups(new KeyValuePair<string, string>(group, value));
        }

        public static Match GetMatchWithGroups(params KeyValuePair<string, string>[] pairs)
        {
            var pattern = string.Empty;
            var input = string.Empty;

            foreach (var pair in pairs)
            {
                pattern += $@"\[(?<{pair.Key}>.*)\]";
                input += $"[{pair.Value}]";
            }

            return Regex.Match(input, pattern);
        }
    }
}
