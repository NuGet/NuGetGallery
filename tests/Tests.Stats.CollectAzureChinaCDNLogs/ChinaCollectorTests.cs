// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;
using Stats.CollectAzureChinaCDNLogs;
using Xunit;

namespace Tests.Stats.CollectAzureChinaCDNLogs
{
    public class ChinaCollectorTests
    {
        public static IEnumerable<object[]> LogData => new object[][] {
            new object[]{ "40.125.202.231,7/27/2017 4:50:09 PM +00:00,GET,\"/v3-flatcontainer/system.net.primitives/index.json\",HTTP/1.1,200,1196,\"-\",\"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\",133,TCP_MISS,118.180.6.168", "1501174209 0 40.125.202.231 0 118.180.6.168 0 TCP_MISS/200 1196 GET /v3-flatcontainer/system.net.primitives/index.json - 133 0 - NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0) na na" },
            new object[]{ "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip", null },
            new object[]{ "66.102.6.172,7/27/2017 4:50:09 PM +00:00,GET,\"/favicon.ico\",HTTP/1.1,200,726,\"-\",\"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google+Favicon\",216,TCP_MISS,150.138.143.19", "1501174209 0 66.102.6.172 0 150.138.143.19 0 TCP_MISS/200 726 GET /favicon.ico - 216 0 - \"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google+Favicon\" na na" },
            new object[]{ "66.102.6.172,7/27/2017 4:50:09 PM +00:00,GET,\"/favicon.ico\",HTTP/1.1,200,726,\"-\",\"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google,Favicon\",216,TCP_MISS,150.138.143.19", "1501174209 0 66.102.6.172 0 150.138.143.19 0 TCP_MISS/200 726 GET /favicon.ico - 216 0 - \"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google,Favicon\" na na" },
            new object[]{ "1.2.3.4,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1", "1554566420 0 1.2.3.4 0 4.3.2.1 0 MISS/200 2044843 GET /v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg - 796 0 NULL \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0,VS Enterprise/15.0)\" na na" },
            new object[]{ "127.0.0.1,1/1/2020 1:23:45 PM +00:00,GET,\"/?q=foo(\"bar\", 2)\",HTTP/1.1,123,456,\"http://nuget.test/?\",\"Mozilla/4.0\",1,HIT,127.0.0.2", null },
        };

        [Theory]
        [MemberData(nameof(LogData))]
        public void TransformRawLogLine(string input, string expectedOutput)
        {
            var collector = new ChinaStatsCollector(
                Mock.Of<ILogSource>(),
                Mock.Of<ILogDestination>(),
                Mock.Of<ILogger<ChinaStatsCollector>>());

            var transformedInput = collector.TransformRawLogLine(input);
            string output = transformedInput == null ? null : transformedInput.ToString();
            Assert.Equal(expectedOutput, output);
        }

        public static IEnumerable<object[]> InputOnlyLogData => LogData.Select(x => new[] { x[0] });

        [Theory]
        [MemberData(nameof(InputOnlyLogData))]
        public void CdnLogEntryParserIntegration(string input)
        {
            var collector = new ChinaStatsCollector(
                Mock.Of<ILogSource>(),
                Mock.Of<ILogDestination>(),
                Mock.Of<ILogger<ChinaStatsCollector>>());

            var transformedInput = collector.TransformRawLogLine(input);
            if (transformedInput == null)
            {
                return;
            }
            string output = transformedInput.ToString();
            const int lineNumber = 1;
            var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(lineNumber, output, onErrorAction: null);
            Assert.Contains(transformedInput.XEc_Custom_1, logEntry.CustomField);
            Assert.Contains(transformedInput.CUserAgent, logEntry.UserAgent);
        }

        [Fact]
        public void SkipsMalformedTimestamps()
        {
            const string data =
                "1.2.3.4,malformed,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1";

            var collector = new ChinaStatsCollector(
                Mock.Of<ILogSource>(),
                Mock.Of<ILogDestination>(),
                Mock.Of<ILogger<ChinaStatsCollector>>());

            OutputLogLine transformedInput = null;

            var exception = Record.Exception(() => transformedInput = collector.TransformRawLogLine(data));
            Assert.Null(exception);
            Assert.Null(transformedInput);
        }


        // potential issues are with non-string columns as they are to be parsed for transformation, specifically:
        // timestamp
        // sc-status
        // sc-bytes
        // rs-duration(ms)
        public static IEnumerable<object[]> LogsWithMalformedData => new object[][] {
            new object[] {
                // timestamp
                "1.1.1.1,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1\n" +
                "1.1.1.1,malformed,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1",
                1
            },
            new object[] {
                // sc-status
                "1.1.1.2,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1\n" +
                "1.1.1.2,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,malformed,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1",
                1
            },
            new object[] {
                // sc-bytes
                "1.1.1.3,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1\n" +
                "1.1.1.3,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,malformed,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1",
                1
            },
            new object[] {
                // rs-duration(ms)
                "1.1.1.4,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",796,MISS,4.3.2.1\n" +
                "1.1.1.4,4/6/2019 4:00:20 PM +00:00,GET,\"/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\",HTTPS,200,2044843,\"NULL\",\"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\",malformed,MISS,4.3.2.1",
                1
            },
        };

        [Theory]
        [MemberData(nameof(LogsWithMalformedData))]
        public async Task SkipsLinesWithMalformedColumns(string data, int expectedOutputLines)
        {
            const string header = "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip\n";
            var sourceUri = new Uri("https://example.com/log1");

            var sourceMock = new Mock<ILogSource>();
            sourceMock
                .Setup(s => s.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync((IEnumerable<Uri>)new List<Uri> { sourceUri });

            sourceMock
                .Setup(s => s.TakeLockAsync(sourceUri, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureBlobLockResult(new Microsoft.WindowsAzure.Storage.Blob.CloudBlob(sourceUri), true, "foo", CancellationToken.None));

            sourceMock
                .Setup(s => s.OpenReadAsync(sourceUri, It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(header + data)));

            var destinationMock = new Mock<ILogDestination>();
            //var outputBuffer = new byte[1024 * 1024];
            var outputStream = new MemoryStream();
            var writeSucceeded = false;
            destinationMock
                .Setup(d => d.TryWriteAsync(It.IsAny<Stream>(), It.IsAny<Action<Stream, Stream>>(), It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token) =>
                {
                    try
                    {
                        writeAction(inputStream, outputStream);
                        writeSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        return new AsyncOperationResult(false, ex);
                    }
                    return new AsyncOperationResult(true, null);
                });

            var collector = new ChinaStatsCollector(
                sourceMock.Object,
                destinationMock.Object,
                Mock.Of<ILogger<ChinaStatsCollector>>());

            await collector.TryProcessAsync(
                maxFileCount: 10,
                fileNameTransform: s => s,
                sourceContentType: ContentType.Text,
                destinationContentType: ContentType.Text,
                CancellationToken.None);

            string[] outputLines = null;

            // need to reopen closed stream
            var outputBuffer = outputStream.ToArray();
            outputStream = new MemoryStream(outputBuffer);
            using (var streamReader = new StreamReader(outputStream))
            {
                outputLines = streamReader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }

            Assert.True(writeSucceeded);
            Assert.NotEmpty(outputLines);
            Assert.Equal("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1", outputLines[0]);
            Assert.Equal(expectedOutputLines, outputLines.Length - 1); // excluding header
        }
    }
}
 