// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.AzureCdnLogs.Common;
using Stats.LogInterpretation;

namespace Stats.ImportAzureCdnStatistics
{
    public class PlatformDimension
    {
        private const string _zeroString = "0";
        private static readonly UserAgentParser _parser;

        static PlatformDimension()
        {
            _parser = new UserAgentParser();
        }

        public int Id { get; set; }
        public string OSFamily { get; set; }
        public string Major { get; set; }
        public string Minor { get; set; }
        public string Patch { get; set; }
        public string PatchMinor { get; set; }

        public static PlatformDimension FromUserAgent(ITrackUserAgent packageStatistics)
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
                result.Major = string.IsNullOrWhiteSpace(parsed.Major) ? _zeroString : parsed.Major;
                result.Minor = string.IsNullOrWhiteSpace(parsed.Minor) ? _zeroString : parsed.Minor;
                result.Patch = string.IsNullOrWhiteSpace(parsed.Patch) ? _zeroString : parsed.Patch;
                result.PatchMinor = string.IsNullOrWhiteSpace(parsed.PatchMinor) ? _zeroString : parsed.PatchMinor;
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
            get { return new PlatformDimension { Id = DimensionId.Unknown, OSFamily = "(unknown)", Major = _zeroString, Minor = _zeroString, Patch = _zeroString, PatchMinor = _zeroString }; }
        }
    }
}