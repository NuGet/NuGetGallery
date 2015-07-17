// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using UAParser;

namespace Stats.ImportAzureCdnStatistics
{
    public class ClientDimension
    {
        private static readonly Parser _parser;

        static ClientDimension()
        {
            _parser = Parser.GetDefault();
        }

        public int Id { get; set; }
        public string ClientName { get; set; }
        public string Major { get; set; }
        public string Minor { get; set; }
        public string Patch { get; set; }

        public static ClientDimension FromPackageStatistic(PackageStatistics packageStatistics)
        {
            if (string.IsNullOrEmpty(packageStatistics.UserAgent))
            {
                return Unknown;
            }

            ClientDimension result;
            var parsed = _parser.ParseUserAgent(packageStatistics.UserAgent);
            if (parsed != null)
            {
                result = new ClientDimension();
                result.ClientName = parsed.Family;
                result.Major = parsed.Major;
                result.Minor = parsed.Minor;
                result.Patch = parsed.Patch;
            }
            else result = Unknown;

            return result;
        }

        public static ClientDimension Unknown
        {
            get { return new ClientDimension { Id = 1, ClientName = "(unknown)" }; }
        }
    }
}