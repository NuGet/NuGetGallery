// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
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
        private JsonSerializerOptions _jsonOptions = null;

        public ChinaStatsCollector(
            ILogSource source,
            ILogDestination destination,
            ILogger<ChinaStatsCollector> logger,
            bool writeHeader,
            bool addSourceFilenameColumn)
            : base(source, destination, logger, writeHeader, addSourceFilenameColumn)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
        }

        public override OutputLogLine TransformRawLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // Ignore empty lines or the header
                return null;
            }

            AfdLogLine parsedLine = ParseLine(line);

            if (parsedLine == null)
            {
                // Parsing failed, already logged
                return null;
            }

            const string notAvailableString = "na";
            const string notAvailableInt = "0";
            const string noStringValue = "-";

            string timeStamp2 = ToUnixTimeStamp(parsedLine.Time);

            // Global status code format: cache status + "/" + HTTP status code
            // China status code format: HTTP status code
            var scstatus = parsedLine.Properties.CacheStatus + "/" + parsedLine.Properties.HttpStatusCode;

            var durationMilliseconds = noStringValue;
            if (parsedLine.Properties.TimeTaken != null && double.TryParse(parsedLine.Properties.TimeTaken, out double timeTakenSeconds))
            {
                durationMilliseconds = ((int)(timeTakenSeconds * 1000)).ToString(CultureInfo.InvariantCulture);
            }

            return new OutputLogLine(timestamp: timeStamp2,
                timetaken: notAvailableInt,
                cip: parsedLine.Properties.ClientIp,
                filesize: notAvailableInt,
                sip: parsedLine.Properties.OriginIp,
                sport: notAvailableInt,
                scstatus: scstatus,
                scbytes: parsedLine.Properties.ResponseBytes,
                csmethod: parsedLine.Properties.HttpMethod,
                csuristem: parsedLine.Properties.RequestUri, // OK to keep full URL
                rsduration: durationMilliseconds,
                rsbytes: notAvailableInt,
                creferrer: string.IsNullOrWhiteSpace(parsedLine.Properties.Referer) ? noStringValue : parsedLine.Properties.Referer,
                cuseragent: string.IsNullOrWhiteSpace(parsedLine.Properties.UserAgent) ? noStringValue : parsedLine.Properties.UserAgent,
                customerid: notAvailableString,
                xeccustom1: $"\"SSL-Protocol: {parsedLine.Properties.SecurityProtocol} SSL-Cipher: {parsedLine.Properties.SecurityCipher} SSL-Curves: {parsedLine.Properties.SecurityCurves}\""
               );
        }

        public override async Task<bool> VerifyStreamAsync(Stream stream)
        {
            const int LinesToCheck = 10;
            using (var reader = new StreamReader(stream))
            {
                int lineIndex = 0;
                string line;

                while (lineIndex++ < LinesToCheck && (line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        AfdLogLine parsedLine = ParseLine(line);
                        if (parsedLine != null) {
                            return true;
                        }
                    }
                }

                // if in the first few lines we couldn't find any parseable line, consider it invalid
                return false;
            }
        }

        private AfdLogLine ParseLine(string line)
        {
            AfdLogLine parsedLine = null;

            try
            {
                parsedLine = JsonSerializer.Deserialize<AfdLogLine>(line, _jsonOptions);
            }
            catch (Exception ex)
            {
                // Ignore lines we can't parse. Not passing exception as the first argument, so it won't end up in exceptions table
                _logger.LogError("Skipping malformed line: {Content}. Exception: {@Exception}", line, ex);
                return null;
            }

            if (parsedLine == null || parsedLine.Properties == null)
            {
                _logger.LogError("Skipping malformed line: {Content}", line);
                return null;
            }

            return parsedLine;
        }
    }
}
