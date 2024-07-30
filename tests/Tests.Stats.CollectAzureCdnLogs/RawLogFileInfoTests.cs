// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Stats.AzureCdnLogs.Common;
using Stats.CollectAzureCdnLogs;
using Xunit;

namespace Tests.Stats.CollectAzureCdnLogs
{
    public class RawLogFileInfoTests
    {
        [Fact]
        public void CorrectlyParsesFilesPendingDownload()
        {
            var uri = new Uri("ftp://someserver/logs/wpc_A000_20150603_0058.log.gz.download");
            var rawLogFile = new RawLogFileInfo(uri);

            Assert.True(rawLogFile.IsPendingDownload);
            Assert.Equal(uri, rawLogFile.Uri);
            Assert.Equal("wpc_A000_20150603_0058.log.gz.download", rawLogFile.FileName);
            Assert.Equal("A000", rawLogFile.AzureCdnAccountNumber);
            Assert.Equal(AzureCdnPlatform.HttpLargeObject, rawLogFile.AzureCdnPlatform);
            Assert.Equal(FileExtensions.RawLog + FileExtensions.Gzip + FileExtensions.Download, rawLogFile.Extension);
            Assert.Equal("application/x-gzip", rawLogFile.ContentType);
            Assert.Equal(DateTime.ParseExact("20150603", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), rawLogFile.GeneratedDate);
            Assert.Equal(58, rawLogFile.RollingFileNumber);
            Assert.Equal("wpc_A000_20150603_0058.log.gz.download", rawLogFile.ToString());
        }

        [Fact]
        public void CorrectlyParsesValidCdnRawLogFileName()
        {
            var uri = new Uri("ftp://someserver/logs/wpc_A000_20150603_0058.log.gz");
            var rawLogFile = new RawLogFileInfo(uri);

            Assert.False(rawLogFile.IsPendingDownload);
            Assert.Equal(uri, rawLogFile.Uri);
            Assert.Equal("wpc_A000_20150603_0058.log.gz", rawLogFile.FileName);
            Assert.Equal("A000", rawLogFile.AzureCdnAccountNumber);
            Assert.Equal(AzureCdnPlatform.HttpLargeObject, rawLogFile.AzureCdnPlatform);
            Assert.Equal(FileExtensions.RawLog + FileExtensions.Gzip, rawLogFile.Extension);
            Assert.Equal("application/x-gzip", rawLogFile.ContentType);
            Assert.Equal(DateTime.ParseExact("20150603", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), rawLogFile.GeneratedDate);
            Assert.Equal(58, rawLogFile.RollingFileNumber);
            Assert.Equal("wpc_A000_20150603_0058.log.gz", rawLogFile.ToString());
        }


        [Fact]
        public void ThrowsWhenInvalidUri()
        {
            Assert.Throws<ArgumentNullException>(() => new RawLogFileInfo(null));
        }

        [Theory]
        [InlineData("ftp://someserver/logs/wpc_A000_20150603.log")]
        [InlineData("ftp://someserver/logs/wpc_A000_20150603_aa00.log.gz")]
        [InlineData("ftp://someserver/logs/wpc_A000_20150603_0058.log")]
        [InlineData("ftp://someserver/logs/wpc_A000_20151342_0058.log.gz")]
        [InlineData("ftp://someserver/logs/wpc_A000_20150603_0058.log.download")]
        [InlineData("ftp://someserver/logs/wpc_A000_20150603_0058.log.gz.tmp")]
        public void ThrowsWhenInvalidRawLogFileName(string uriString)
        {
            Assert.Throws<InvalidRawLogFileNameException>(() => new RawLogFileInfo(new Uri(uriString)));
        }
    }
}
