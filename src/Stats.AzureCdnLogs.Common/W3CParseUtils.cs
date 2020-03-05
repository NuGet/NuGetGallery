// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Stats.AzureCdnLogs.Common
{
    public static class W3CParseUtils
    {
        private const int LogLineRecordLength = 17;

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

            // In case there are less records than the expected, fill with empty string
            if (records.Count < LogLineRecordLength)
            {
                var recordsToAdd = LogLineRecordLength - records.Count;
                records.AddRange(Enumerable.Repeat(string.Empty, recordsToAdd));
            }

            return records.ToArray();
        }

        public static bool RecordContainsData(string record)
        {
            return !string.IsNullOrWhiteSpace(record) && record != "-" && record != "\"-\"";
        }
    }
}