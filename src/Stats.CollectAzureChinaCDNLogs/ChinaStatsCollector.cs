// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CollectAzureChinaCDNLogs
{
    /// <summary>
    /// An implementation of the <see cref="Stats.AzureCdnLogs.Common.Collect.Collector" for China CDN logs./>
    /// </summary>
    public class ChinaStatsCollector : Collector
    {
        private const string Header = "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip";
        private const int ExpectedFields = 12;
        //representation of the header of the log files from China CDN
        private enum ChinaLogHeaderFields
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

        public override OutputLogLine TransformRawLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || 
                string.IsNullOrEmpty(line) || 
                line.Trim().StartsWith("c-ip", ignoreCase: true, culture: CultureInfo.InvariantCulture))
            {
                // Ignore empty lines or the header
                return null;
            }

            // Skip malformed lines.
            var segments = ExtensionsUtils.GetSegmentsFromCSV(line);
            if (segments.Length < ExpectedFields)
            {
                _logger.LogError(
                    "Skipping malformed raw log line with {Segments} segments and content {Content}.",
                    segments.Length,
                    line);
                return null;
            }

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
    }
}
