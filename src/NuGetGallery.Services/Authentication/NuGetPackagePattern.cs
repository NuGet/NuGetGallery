// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;

namespace NuGetGallery.Authentication
{
    public static class NuGetPackagePattern
    {
        public const string AllInclusivePattern = "*";

        /// <summary>
        /// Compares the string against a given pattern.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="globPattern">The pattern to match, where "*" means any sequence of characters, and "?" means any single character.</param>
        /// <returns><c>true</c> if the string matches the given pattern; otherwise <c>false</c>.</returns>
        public static bool MatchesPackagePattern(this string str, string globPattern)
        {
            return RegexEx.CreateWithTimeout(
                "^" + Regex.Escape(globPattern).Replace(@"\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).IsMatch(str);
        }
    }
}