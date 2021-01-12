// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
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

            var tranformedinput = collector.TransformRawLogLine(input);
            string output = tranformedinput == null ? null : tranformedinput.ToString();
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

            var tranformedInput = collector.TransformRawLogLine(input);
            if (tranformedInput == null)
            {
                return;
            }
            string output = tranformedInput.ToString();
            const int lineNumber = 1;
            var logEntry = CdnLogEntryParser.ParseLogEntryFromLine(lineNumber, output, onErrorAction: null);
            Assert.Contains(tranformedInput.XEc_Custom_1, logEntry.CustomField);
            Assert.Contains(tranformedInput.CUserAgent, logEntry.UserAgent);
        }
    }
}
 