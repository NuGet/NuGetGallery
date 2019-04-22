// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Stats.AzureCdnLogs.Common
{
    public static class ExtensionsUtils
    {
        public static string[] GetSegmentsFromCSV(string line, char delimiter)
        {
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrEmpty(line))
            {
                return new string[0];
            }
            if(delimiter == ',')
            {
                return GetSegmentsFromCSV(line);
            }
            return line.Split(delimiter).Select(s => s.Trim()).ToArray();
        }

        public static string[] GetSegmentsFromCSV(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrEmpty(line))
            {
                return new string[0];
            }
            var segments = line.Split(',').Select(s => s.Trim()).ToArray();
            List<string> result = new List<string>();
            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].Contains("\"") || (segments[i].StartsWith("\"") && segments[i].EndsWith("\"")))
                {
                    result.Add(segments[i]);
                }
                else
                {
                    //this case is when an entry is like 
                    //""Mozilla/5.0+(X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/11.0.1111.11+Safari/537.36+Google+Favicon""
                    //Note the comma inside of the entry
                    string resultInt = segments[i++];
                    while (i < segments.Length && !segments[i].EndsWith("\""))
                    {
                        resultInt += "," + segments[i++];
                    }
                    if (i < segments.Length) { resultInt += "," + segments[i]; }
                    result.Add(resultInt);
                }
            }
            return result.Select(s => s.Replace("\"", "")).ToArray();
        }
    }
}
