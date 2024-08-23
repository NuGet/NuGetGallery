// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Stats.AzureCdnLogs.Common
{
    public static class W3CParseUtils
    {
        public static string[] GetLogLineRecords(string line)
        {
            var records = new List<string>();

            var startIndex = 0;
            var betweenQuotes = false;
            var characterCount = line.Length;

            for (var i = 0; i < characterCount; i++)
            {
                char character = line[i];

                if (i + 1 == characterCount)
                {
                    // reached end of the line
                    var record = line.Substring(startIndex, characterCount - startIndex);
                    records.Add(record);
                }
                else if (character == '"')
                {
                    betweenQuotes = !betweenQuotes;
                    if (betweenQuotes)
                    {
                        startIndex = i;
                    }
                }
                else if (character == ' ' && !betweenQuotes)
                {
                    var record = line.Substring(startIndex, i - startIndex);
                    records.Add(record);
                    startIndex = i + 1;
                }
            }

            return records.ToArray();
        }

        public static bool RecordContainsData(string record)
        {
            return !string.IsNullOrWhiteSpace(record) && record != "-" && record != "\"-\"";
        }
    }
}