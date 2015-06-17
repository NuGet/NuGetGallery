// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Stats.ParseAzureCdnLogs
{
    internal static class W3CParseUtils
    {
        public static string[] GetLogLineRecords(string line)
        {
            var records = new List<string>();

            int startIndex = 0;
            bool openedQuotes = false;
            var characterCount = line.Length;

            for (int i = 0; i < characterCount; i++)
            {
                char character = line[i];
                if (character == ' ')
                {
                    if (!openedQuotes)
                    {
                        string record = line.Substring(startIndex, i - startIndex);
                        records.Add(record);
                        startIndex = i + 1;

                        if (i + 1 < characterCount && line[i + 1] == '"')
                        {
                            // the next space character encountered could be between quotes
                            openedQuotes = true;
                        }
                    }
                    else
                    {
                        // quotes are open, verify if they were closed
                        if (line[i - 1] == '"')
                        {
                            string record = line.Substring(startIndex, i - startIndex);
                            records.Add(record);
                            startIndex = i + 1;
                            openedQuotes = false;
                        }
                    }
                }
                else if (i + 1 == characterCount)
                {
                    // reached end of the line
                    string record = line.Substring(startIndex, characterCount - startIndex);
                    records.Add(record);
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