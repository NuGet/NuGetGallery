// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NuGetGallery.Areas.Admin
{
    public static class Helpers
    {
        public static IReadOnlyList<string> ParseQueryToLines(string query)
        {
            var lines = new List<string>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return lines;
            }

            // Collapse redundant spaces.
            var normalizedQuery = Regex.Replace(
                query,
                @"[^\S\r\n]+",
                " ",
                RegexOptions.None,
                TimeSpan.FromSeconds(10));

            // Split lines and trim.
            var uniqueLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var stringReader = new StringReader(normalizedQuery))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.Length > 0 && uniqueLines.Add(trimmedLine))
                    {
                        lines.Add(trimmedLine);
                    }
                }
            }

            return lines;
        }
    }
}