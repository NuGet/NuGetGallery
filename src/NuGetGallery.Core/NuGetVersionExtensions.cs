// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private const RegexOptions SemanticVersionRegexFlags = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        private static readonly Regex SemanticVersionRegex = new Regex(@"^(?<Version>\d+(\s*\.\s*\d+){0,3})(?<Release>-[a-z][0-9a-z-]*)?$", SemanticVersionRegexFlags);

        public static string ToNormalizedStringSafe(this NuGetVersion self)
        {
            return self != null ? self.ToNormalizedString() : string.Empty;
        }

        public static string ToFullStringSafe(this NuGetVersion self)
        {
            // For SemVer2 versions, we want to show the full string including metadata in the UI.
            // However, the rest of the version string should be normalized, 
            // which NuGetVersion.ToFullString does not do for non-SemVer2 packages.
            // Hence the conditional call to ToNormalizedString for non-SemVer2 packages.
            return self != null ? (self.IsSemVer2 ? self.ToFullString() : self.ToNormalizedString()) : string.Empty;
        }

        public static bool IsValidVersionForLegacyClients(this NuGetVersion self)
        {
            var match = SemanticVersionRegex.Match(self.ToString().Trim());

            return match.Success;
        }
    }
}