// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using UAParser;

namespace Stats.ImportAzureCdnStatistics
{
    public class PlatformDimension
    {
        private static readonly Parser _parser;

        static PlatformDimension()
        {
            _parser = Parser.GetDefault();
        }

        public int Id { get; set; }
        public string OSFamily { get; set; }
        public string Major { get; set; }
        public string Minor { get; set; }
        public string Patch { get; set; }
        public string PatchMinor { get; set; }

        public static PlatformDimension FromPackageStatistic(PackageStatistics packageStatistics)
        {
            if (string.IsNullOrEmpty(packageStatistics.UserAgent))
            {
                return Unknown;
            }

            var dimension = Parse(packageStatistics.UserAgent);
            return dimension;
        }

        private static PlatformDimension Parse(string userAgent)
        {
            PlatformDimension result;

            var parsed = _parser.ParseOS(userAgent);
            if (parsed != null)
            {
                result = new PlatformDimension();
                result.OSFamily = parsed.Family;
                result.Major = parsed.Major;
                result.Minor = parsed.Minor;
                result.Patch = parsed.Patch;
                result.PatchMinor = parsed.PatchMinor;
            }
            else
            {
                // Unknown platform
                result = Unknown;
            }
            return result;
        }

        public static PlatformDimension Unknown
        {
            get { return new PlatformDimension { Id = DimensionId.Unknown, OSFamily = "(unknown)" }; }
        }
    }
}