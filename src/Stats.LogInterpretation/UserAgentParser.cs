// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UAParser;

namespace Stats.LogInterpretation
{
    public class UserAgentParser
    {
        private static readonly Parser _defaultParser;
        private static readonly Parser _knownClientsParser;
        private static readonly Parser _knownClientsInChinaParser;

        static UserAgentParser()
        {
            _defaultParser = Parser.GetDefault();

            var yaml = ReadKnownClientsYaml();
            _knownClientsParser = Parser.FromYaml(yaml);

            var patchedYaml = AddSupportForChinaCdn(yaml);
            _knownClientsInChinaParser = Parser.FromYaml(patchedYaml);
        }

        private static string AddSupportForChinaCdn(string yaml)
        {
            // Seems like user agent headers from requests hitting the China CDN endpoints 
            // are using '+' characters instead of whitespace characters

            var patchedYaml = Regex.Replace(
                yaml,
                @"(?:[:]\s'\()+([\w-.\s]+)(?:\))+", // Look for any matches of : '(user agent)
                ReplaceWhitespaceWithPlusSign, // Replace whitespace ' ' character with '+' character in the user agent matches
                RegexOptions.Compiled);

            return patchedYaml;
        }

        private static string ReplaceWhitespaceWithPlusSign(Match match)
        {
            // The + sign needs to be escaped by a \ 
            // as it is output to another regex in YAML.
            return ": '(" + match.Groups[1].Value.Replace(" ", @"\+") + ")";
        }

        private static string ReadKnownClientsYaml()
        {
            string yaml;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("knownclients.yaml"))
            using (var reader = new StreamReader(stream))
            {
                yaml = reader.ReadToEnd();
            }
            return yaml;
        }

        public UserAgent ParseUserAgent(string userAgent)
        {
            // try custom parser first
            var parsedResult = _knownClientsParser.ParseUserAgent(userAgent);

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to China parser
                parsedResult = _knownClientsInChinaParser.ParseUserAgent(userAgent);
            }

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to default parser
                parsedResult = _defaultParser.ParseUserAgent(userAgent);
            }
            return parsedResult;
        }

        public OS ParseOS(string userAgent)
        {
            // try custom parser first
            var parsedResult = _knownClientsParser.ParseOS(userAgent);

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to China parser
                parsedResult = _knownClientsInChinaParser.ParseOS(userAgent);
            }

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to default parser
                parsedResult = _defaultParser.ParseOS(userAgent);
            }

            return parsedResult;
        }

        public Device ParseDevice(string userAgent)
        {
            // try custom parser first
            var parsedResult = _knownClientsParser.ParseDevice(userAgent);

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to China parser
                parsedResult = _knownClientsInChinaParser.ParseDevice(userAgent);
            }

            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to default parser
                parsedResult = _defaultParser.ParseDevice(userAgent);
            }
            return parsedResult;
        }
    }
}