﻿using System;
using System.Globalization;
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
            return version == null ? null : new SemanticVersion(version).ToNormalizedString();
        }

        public static string ToNormalizedString(this SemanticVersion self)
        {
            // SemanticVersion normalizes the missing components to 0.
            return String.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1}.{2}{3}{4}",
                self.Version.Major,
                self.Version.Minor,
                self.Version.Build,
                self.Version.Revision > 0 ? ("." + self.Version.Revision.ToString(CultureInfo.InvariantCulture)) : String.Empty,
                !String.IsNullOrEmpty(self.SpecialVersion) ? ("-" + self.SpecialVersion) : String.Empty);
        }

        public static string ToDisplayString(this SemanticVersion self)
        {
            return self != null ? self.ToNormalizedString() : String.Empty;
        }
    }
}