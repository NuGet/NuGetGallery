// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NuGet;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a normalized, spec-compatible (with NuGet extensions), Semantic Version as defined at http://semver.org
    /// </summary>
    public static class SemanticVersionExtensions
    {
        public static string Normalize(string version)
        {
            SemanticVersion parsed;
            if (!SemanticVersion.TryParse(version, out parsed))
            {
                return version;
            }
            return parsed.ToNormalizedString();
        }

        public static string ToNormalizedString(this SemanticVersion self)
        {
            // SemanticVersion normalizes the missing components to 0.
            return String.Format(CultureInfo.InvariantCulture,
                "{0}.{1}.{2}{3}{4}",
                self.Version.Major,
                self.Version.Minor,
                self.Version.Build,
                self.Version.Revision > 0 ? ("." + self.Version.Revision.ToString(CultureInfo.InvariantCulture)) : String.Empty,
                !String.IsNullOrEmpty(self.SpecialVersion) ? ("-" + self.SpecialVersion) : String.Empty);

        }

        public static string ToNormalizedStringSafe(this SemanticVersion self)
        {
            return self != null ? self.ToNormalizedString() : String.Empty;
        }
    }
}