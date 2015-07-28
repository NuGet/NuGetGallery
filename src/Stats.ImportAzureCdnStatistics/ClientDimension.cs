// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Stats.AzureCdnLogs.Common;

namespace Stats.ImportAzureCdnStatistics
{
    public class ClientDimension
    {
        private const string _other = "other";
        private const string _zeroString = "0";
        private static readonly UserAgentParser _parser;

        static ClientDimension()
        {
            _parser = new UserAgentParser();
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
                if (string.Equals(_other, parsed.Family, StringComparison.OrdinalIgnoreCase))
                {
                    return Unknown;
                }
                result = new ClientDimension();
                result.ClientName = parsed.Family;
                result.Major = string.IsNullOrWhiteSpace(parsed.Major) ? _zeroString : parsed.Major;
                result.Minor = string.IsNullOrWhiteSpace(parsed.Minor) ? _zeroString : parsed.Minor;
                result.Patch = string.IsNullOrWhiteSpace(parsed.Patch) ? _zeroString : parsed.Patch;
            }
            else result = Unknown;

            return result;
        }

        private static ClientDimension Unknown
        {
            get { return new ClientDimension { Id = DimensionId.Unknown, ClientName = "(unknown)", Major = _zeroString, Minor = _zeroString, Patch = _zeroString }; }
        }
    }
}