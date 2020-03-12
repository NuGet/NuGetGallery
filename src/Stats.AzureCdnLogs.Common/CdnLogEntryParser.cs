// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Stats.AzureCdnLogs.Common
{
    public static class CdnLogEntryParser
    {
        private static readonly DateTime _unixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static CdnLogEntry ParseLogEntryFromLine(int lineNumber, string line, Action<Exception, int> onErrorAction)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            // ignore comment rows (i.e., first row listing the column headers
            if (line.StartsWith("#"))
            {
                return null;
            }

            // disregard 404's
            if (line.Contains("TCP_MISS/404"))
            {
                return null;
            }

            // columns are space-separated
            var columns = W3CParseUtils.GetLogLineRecords(line);

            var entry = new CdnLogEntry();

            try
            {
                // timestamp
                entry.EdgeServerTimeDelivered = FromUnixTimestamp(columns[0]);

                // time-taken
                TrySetIntProperty(value => entry.EdgeServerTimeTaken = value, columns[1]);

                // c-ip
                TrySetStringProperty(value => entry.ClientIpAddress = value, columns[2]);

                // filesize
                TrySetLongProperty(value => entry.FileSize = value, columns[3]);

                // s-ip
                TrySetStringProperty(value => entry.EdgeServerIpAddress = value, columns[4]);

                // s-port
                TrySetIntProperty(value => entry.EdgeServerPort = value, columns[5]);

                // sc-status
                TrySetStringProperty(value => entry.CacheStatusCode = value, columns[6]);

                // sc-bytes
                TrySetLongProperty(value => entry.EdgeServerBytesSent = value, columns[7]);

                // cs-method
                TrySetStringProperty(value => entry.HttpMethod = value, columns[8]);

                // cs-uri-stem
                TrySetStringProperty(value => entry.RequestUrl = value, columns[9]);

                // skip column 10, it just contains the '-' character

                // rs-duration
                TrySetIntProperty(value => entry.RemoteServerTimeTaken = value, columns[11]);

                // rs-bytes
                TrySetLongProperty(value => entry.RemoteServerBytesSent = value, columns[12]);

                // c-referrer
                TrySetStringProperty(value => entry.Referrer = value, columns[13]);

                // c-user-agent
                TrySetStringProperty(value => entry.UserAgent = value, columns[14]);

                // customer-id
                TrySetStringProperty(value => entry.CustomerId = value, columns[15]);

                // x-ec_custom-1
                TrySetStringProperty(value => entry.CustomField = value, columns[16]);
            }
            catch (FormatException e)
            {
                // skip this line but log the error
                if (onErrorAction == null)
                {
                    throw;
                }
                else
                {
                    onErrorAction.Invoke(e, lineNumber);

                    return null;
                }
            }
            catch (IndexOutOfRangeException e)
            {
                // skip this line but log the error
                if (onErrorAction == null)
                {
                    throw;
                }
                else
                {
                    onErrorAction.Invoke(e, lineNumber);

                    return null;
                }
            }

            // Try to exclude non-200 level HTTP status codes. If the format is unexpected, process the log entry as
            // usual. We don't want status code format changes to disrupt statistics flowing, even if it means there a
            // small margin of error caused by non-200 HTTP status codes.
            if (entry.CacheStatusCode != null)
            {
                // Previously, we were not correctly converting logs from China CDN to the format used by Global CDN, so we must support both formats.
                // Global format: cache status + "/" + HTTP status code
                // Global example: "TCP_MISS/504"
                // China format: HTTP status code
                // China example: "504"
                var slashIndex = entry.CacheStatusCode.LastIndexOf('/');
                uint httpStatusCode;
                if (slashIndex + 1 < entry.CacheStatusCode.Length
                    && uint.TryParse(entry.CacheStatusCode.Substring(slashIndex + 1), out httpStatusCode)
                    && (httpStatusCode < 200 || httpStatusCode >= 300))
                {
                    return null;
                }
            }

            return entry;
        }

        private static void TrySetLongProperty(Action<long?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(long.Parse(record));
            }
        }

        private static void TrySetIntProperty(Action<int?> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(int.Parse(record));
            }
        }

        private static void TrySetStringProperty(Action<string> propertySetter, string record)
        {
            if (W3CParseUtils.RecordContainsData(record))
            {
                propertySetter(record);
            }
            else
            {
                propertySetter(string.Empty);
            }
        }

        private static DateTime FromUnixTimestamp(string unixTimestamp)
        {
            // Unix timestamp is seconds past epoch
            var secondsPastEpoch = double.Parse(unixTimestamp);
            return _unixTimestamp + TimeSpan.FromSeconds(secondsPastEpoch);
        }
    }
}