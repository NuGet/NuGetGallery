// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Stats.AzureCdnLogs.Common.Collect;
using Microsoft.Extensions.Logging;

namespace Stats.CollectAzureChinaCDNLogs
{
    /// <summary>
    /// An implementation of the <see cref="Stats.AzureCdnLogs.Common.Collect.Collector" for China CDN logs./>
    /// </summary>
    public class ChinaStatsCollector : Collector
    {
        const string Header = "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip";
        //representation of the header of the log files from China CDN
        enum ChinaLogHeaderFields
        {
            cip = 0,
            timestamp = 1,
            csmethod = 2,
            csuristem = 3,
            httpver = 4,
            scstatus = 5,
            scbytes = 6,
            creferer = 7,
            cuseragent = 8,
            rsduration = 9,
            hitmiss = 10,
            sip = 11
        }

        public ChinaStatsCollector(ILogSource source, ILogDestination destination, ILogger<ChinaStatsCollector> logger) : base(source, destination, logger)
        {}

        public ChinaStatsCollector()
        { }

        public override OutputLogLine TransformRawLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || 
                string.IsNullOrEmpty(line) || 
                line.Trim().StartsWith("c-ip", ignoreCase: true, culture: System.Globalization.CultureInfo.InvariantCulture))
            {
                // Ignore empty lines or the header
                return null;
            }

            string[] segments = GetSegments(line);
            const string notAvailableString = "na";
            const string notAvailableInt = "0";

            string timestamp = segments[(int)ChinaLogHeaderFields.timestamp];
            DateTime dt = DateTime.Parse(timestamp, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);
            string timeStamp2 = ToUnixTimeStamp(dt);

            // Global status code format: cache status + "/" + HTTP status code
            // China status code format: HTTP status code
            var scstatus = segments[(int)ChinaLogHeaderFields.hitmiss] + "/" + segments[(int)ChinaLogHeaderFields.scstatus];

            return new OutputLogLine(timestamp: timeStamp2,
                timetaken: notAvailableInt,
                cip:segments[(int)ChinaLogHeaderFields.cip],
                filesize: notAvailableInt,
                sip: segments[(int)ChinaLogHeaderFields.sip],
                sport: notAvailableInt,
                scstatus: scstatus,
                scbytes: segments[(int)ChinaLogHeaderFields.scbytes],
                csmethod: segments[(int)ChinaLogHeaderFields.csmethod],
                csuristem: segments[(int)ChinaLogHeaderFields.csuristem],
                rsduration: segments[(int)ChinaLogHeaderFields.rsduration],
                rsbytes: notAvailableInt,
                creferrer: segments[(int)ChinaLogHeaderFields.creferer],
                cuseragent: segments[(int)ChinaLogHeaderFields.cuseragent],
                customerid: notAvailableString,
                xeccustom1: notAvailableString
               );
        }

        public override async Task<bool> VerifyStreamAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                //read the first line
                string firstLine = await reader.ReadLineAsync();
                return firstLine.Trim().Equals(Header, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        private string[] GetSegments(string line)
        {
           if (string.IsNullOrWhiteSpace(line) || string.IsNullOrEmpty(line))
           {
               return null;
           }
           string[] segments = line.Split(',').Select(s=>s.Trim()).ToArray();
            List<string> result = new List<string>();
            for(int i = 0; i < segments.Length; i++)
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
                    while(i < segments.Length && !segments[i].EndsWith("\""))
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
