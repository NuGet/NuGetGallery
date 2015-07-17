// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using UAParser;

namespace Stats.ImportAzureCdnStatistics
{
    public class UserAgentParser
    {
        private static readonly Parser _defaultParser;
        private static readonly Parser _customParser;

        static UserAgentParser()
        {
            _defaultParser = Parser.GetDefault();

            var yaml = ReadKnownClientsYaml();
            _customParser = Parser.FromYaml(yaml);
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
            var parsedResult = _customParser.ParseUserAgent(userAgent);
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
            var parsedResult = _customParser.ParseOS(userAgent);
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
            var parsedResult = _customParser.ParseDevice(userAgent);
            if (string.Equals(parsedResult.Family, "other", StringComparison.InvariantCultureIgnoreCase))
            {
                // fallback to default parser
                parsedResult = _defaultParser.ParseDevice(userAgent);
            }
            return parsedResult;
        }
    }
}