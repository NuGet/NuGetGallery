// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.ParseAzureCdnLogs
{
    public class NuGetClientInfo
    {
        public NuGetClientInfo(string name)
            : this(name, string.Empty)
        {
        }

        public NuGetClientInfo(string name, string category)
        {
            Name = name;
            Category = category;
        }

        public string Name { get; private set; }
        public string Category { get; private set; }

        public virtual int GetMajorVersion(string userAgent)
        {
            if (Category == "NuGet")
            {
                // The following 'IF' condition truncates OS information that may be present at the end of the User Agent string inside braces.
                // OS information may have version information. So, truncating that out helps in a simplified and accurate parsing of UserAgent client minor version.
                // NOTE that, despite truncating OS information, it is assumed that the User Agent string from NuGet clients will always have the major and minor version
                var indexOfOpeningBrace = userAgent.IndexOf("(", StringComparison.Ordinal);
                if (indexOfOpeningBrace != 0)
                {
                    userAgent = userAgent.Substring(0, indexOfOpeningBrace);
                }

                // start 1 character after the slash
                var startIndex = userAgent.IndexOf("/", StringComparison.Ordinal) + 1;

                // To determine string length, subtract (position of first slash, determined as above) from
                // (position of first dot occuring after first slash.
                var indexOfDotAfterSlash = (userAgent + ".").IndexOf(".", startIndex, StringComparison.Ordinal);
                int length = indexOfDotAfterSlash - startIndex;

                // get the major version segment
                var segment = userAgent.Substring(startIndex, length);

                int majorVersion;
                if (int.TryParse(segment, out majorVersion))
                {
                    return majorVersion;
                }
            }
            return 0;
        }

        public virtual int GetMinorVersion(string userAgent)
        {
            if (Category == "NuGet")
            {
                // The following 'IF' condition truncates OS information that may be present at the end of the User Agent string inside braces.
                // OS information may have version information. So, truncating that out helps in a simplified and accurate parsing of UserAgent client minor version.
                // NOTE that, despite truncating OS information, it is assumed that the User Agent string from NuGet clients will always have the major and minor version
                var indexOfOpeningBrace = userAgent.IndexOf("(", StringComparison.Ordinal);
                if (indexOfOpeningBrace != 0)
                {
                    userAgent = userAgent.Substring(0, indexOfOpeningBrace);
                }

                // start 1 character after the first dot after the slash
                var startIndex = userAgent.IndexOf(".", userAgent.IndexOf("/", StringComparison.Ordinal) + 1, StringComparison.Ordinal) + 1;

                // determine string length
                var indexOfDotAfterSlash = (userAgent + ".").IndexOf(".", startIndex, StringComparison.Ordinal);
                var length = indexOfDotAfterSlash - startIndex;

                var segment = userAgent.Substring(startIndex, length);

                int minorVersion;
                if (int.TryParse(segment, out minorVersion))
                {
                    return minorVersion;
                }
            }
            return 0;
        }

        public virtual string GetPlatform(string userAgent)
        {
            if (Category == "NuGet")
            {
                var indexOfOpeningBrace = userAgent.IndexOf("(", StringComparison.Ordinal);
                if (indexOfOpeningBrace != 0)
                {
                    return userAgent.Substring(indexOfOpeningBrace + 1, userAgent.Length - indexOfOpeningBrace - 2);
                }
            }
            return string.Empty;
        }

        public static NuGetClientInfo Browser()
        {
            return new NuGetClientInfo("Other", "Browser");
        }
    }
}