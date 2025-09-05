// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Stats.AzureCdnLogs.Common;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class W3CParseUtilsFacts
    {
        public class TheGetLogLineRecordsMethod
        {
            [Fact]
            public void CanHandleLinesWithoutQuotesInRecords()
            {
                // #Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1
                var line = "1433257489 27 127.0.0.1 1482348 127.0.0.1 443 TCP_HIT/200 1482769 GET http://localhost/packages/packageId/packageVersion.nupkg - 0 844 https://localhost/api/v2/package/packageId/packageVersion userAgent 61800 -";

                var records = W3CParseUtils.GetLogLineRecords(line);

                Assert.Equal(17, records.Count());
                Assert.Equal("1433257489", records[0]);
                Assert.Equal("27", records[1]);
                Assert.Equal("127.0.0.1", records[2]);
                Assert.Equal("1482348", records[3]);
                Assert.Equal("127.0.0.1", records[4]);
                Assert.Equal("443", records[5]);
                Assert.Equal("TCP_HIT/200", records[6]);
                Assert.Equal("1482769", records[7]);
                Assert.Equal("GET", records[8]);
                Assert.Equal("http://localhost/packages/packageId/packageVersion.nupkg", records[9]);
                Assert.Equal("-", records[10]);
                Assert.Equal("0", records[11]);
                Assert.Equal("844", records[12]);
                Assert.Equal("https://localhost/api/v2/package/packageId/packageVersion", records[13]);
                Assert.Equal("userAgent", records[14]);
                Assert.Equal("61800", records[15]);
                Assert.Equal("-", records[16]);
            }

            [Fact]
            public void CanHandleLinesWithQuotesInRecords()
            {
                // #Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1
                var line = "1433257489 27 127.0.0.1 1482348 127.0.0.1 443 TCP_HIT/200 1482769 GET \"http://localhost/packages/packageId/packageVersion.nupkg\" - 0 844 \"https://localhost/api/v2/package/packageId/packageVersion\" \"Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)\" 61800 \"NuGet-Operation: -\"";

                var records = W3CParseUtils.GetLogLineRecords(line);

                Assert.Equal(17, records.Count());
                Assert.Equal("1433257489", records[0]);
                Assert.Equal("27", records[1]);
                Assert.Equal("127.0.0.1", records[2]);
                Assert.Equal("1482348", records[3]);
                Assert.Equal("127.0.0.1", records[4]);
                Assert.Equal("443", records[5]);
                Assert.Equal("TCP_HIT/200", records[6]);
                Assert.Equal("1482769", records[7]);
                Assert.Equal("GET", records[8]);
                Assert.Equal("\"http://localhost/packages/packageId/packageVersion.nupkg\"", records[9]);
                Assert.Equal("-", records[10]);
                Assert.Equal("0", records[11]);
                Assert.Equal("844", records[12]);
                Assert.Equal("\"https://localhost/api/v2/package/packageId/packageVersion\"", records[13]);
                Assert.Equal("\"Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)\"", records[14]);
                Assert.Equal("61800", records[15]);
                Assert.Equal("\"NuGet-Operation: -\"", records[16]);
            }
        }

        public class TheRecordContainsDataMethod
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("\"-\"")]
            [InlineData("-")]
            public void ReturnsFalseForRecordWithoutData(string record)
            {
                bool actual = W3CParseUtils.RecordContainsData(record);
                Assert.False(actual);
            }

            [Theory]
            [InlineData("userAgent")]
            [InlineData("\"NuGet-Operation: -\"")]
            public void ReturnsTrueForRecordWithData(string record)
            {
                bool actual = W3CParseUtils.RecordContainsData(record);
                Assert.True(actual);
            }
        }
    }
}