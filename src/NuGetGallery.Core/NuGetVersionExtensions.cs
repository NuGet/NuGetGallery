// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace NuGetGallery
{
    public static class NuGetVersionNormalizer
    {
        public static string Normalize(string version)
        {
            NuGetVersion parsed;
            if (!NuGetVersion.TryParse(version, out parsed))
            {
                return version;
            }

            return parsed.ToNormalizedString();
        }
    }

    public static class NuGetVersionExtensions
    {
        private const RegexOptions Flags = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        private static readonly Regex SemanticVersionRegex = new Regex(@"^(?<Version>\d+(\s*\.\s*\d+){0,3})(?<Release>-[a-z][0-9a-z-]*)?$", Flags);

        public static string ToNormalizedStringSafe(this NuGetVersion self)
        {
            return self != null ? self.ToNormalizedString() : String.Empty;
        }

        public static bool IsSemVer200(this NuGetVersion self)
        {
            return self.ReleaseLabels.Count() > 1 || self.HasMetadata;
        }

        public static bool IsValidVersionForLegacyClients(this NuGetVersion self)
        {
            var match = SemanticVersionRegex.Match(self.ToString().Trim());

            return match.Success;
        }
    }
}