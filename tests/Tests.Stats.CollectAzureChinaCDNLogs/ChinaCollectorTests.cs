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
using Azure.Storage.Blobs;
using Moq;
using Stats.AzureCdnLogs.Common;
using Stats.AzureCdnLogs.Common.Collect;
using Stats.CollectAzureChinaCDNLogs;
using Xunit;
using Azure.Storage.Blobs.Models;

namespace Tests.Stats.CollectAzureChinaCDNLogs
{
    public class ChinaCollectorTests
    {
        public static IEnumerable<object[]> LogData => new object[][] {
            new object[]{
                "{\"time\": \"2017-07-27T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                "1501174209 0 5.6.7.8 0 9.10.11.12:443 0 TCP_MISS/200 1196 GET https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json - 133 0 - NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0) na \"SSL-Protocol: TLS 1.2 SSL-Cipher: ECDHE-RSA-AES256-GCM-SHA384 SSL-Curves: X25519:prime256v1:secp384r1\""
            },
            new object[]{
                "c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip",
                null
            },
            new object[]{
                "{\"time\": \"2017-07-27T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/favicon.ico\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"726\", \"userAgent\": \"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google+Favicon\", \"clientIp\": \"13.14.15.16\", \"clientPort\": \"24576\", \"socketIp\": \"13.14.15.16\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.216\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"17.18.19.20:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                "1501174209 0 13.14.15.16 0 17.18.19.20:443 0 TCP_MISS/200 726 GET https://nugetdev.azure.cn:443/favicon.ico - 216 0 - \"Mozilla/5.0+ X11;+Linux+x86_64)+AppleWebKit/537.36+(KHTML,+like+Gecko)+Chrome/49.0.2623.75+Safari/537.36+Google+Favicon\" na \"SSL-Protocol: TLS 1.2 SSL-Cipher: ECDHE-RSA-AES256-GCM-SHA384 SSL-Curves: X25519:prime256v1:secp384r1\""
            },
            new object[]{
                "{\"time\": \"2019-04-06T16:00:20.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"2044843\", \"userAgent\": \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0,VS Enterprise/15.0)\", \"clientIp\": \"1.2.3.4\", \"clientPort\": \"24576\", \"socketIp\": \"1.2.3.4\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.796\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"4.3.2.1:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"NULL\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                "1554566420 0 1.2.3.4 0 4.3.2.1:443 0 MISS/200 2044843 GET https://nugetdev.azure.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg - 796 0 NULL \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0,VS Enterprise/15.0)\" na \"SSL-Protocol: TLS 1.2 SSL-Cipher: ECDHE-RSA-AES256-GCM-SHA384 SSL-Curves: X25519:prime256v1:secp384r1\""
            },
        };

        [Theory]
        [MemberData(nameof(LogData))]
        public void TransformRawLogLine(string input, string expectedOutput)
        {
            var collector = new ChinaStatsCollector(
                Mock.Of<ILogSource>(),
                Mock.Of<ILogDestination>(),
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: true,
                addSourceFilenameColumn: false);

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
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: true,
                addSourceFilenameColumn: false);

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
                "{\"time\": \"malformed\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"fdkfghdksfljghu\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"3291\", \"userAgent\": \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\", \"clientIp\": \"1.2.3.4\", \"clientPort\": \"24576\", \"socketIp\": \"1.2.3.4\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.137\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"PRIVATE_NOSTORE\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://testorigin.blob.core.chinacloudapi.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"originIp\": \"4.3.2.1:443\", \"originName\": \"testorigin.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}";

            var collector = new ChinaStatsCollector(
                Mock.Of<ILogSource>(),
                Mock.Of<ILogDestination>(),
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: true,
                addSourceFilenameColumn: false);

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
                "{\"time\": \"2017-07-26T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}\n" +
                "{\"time\": \"malformed\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                1
            },
            new object[] {
                // httpStatusCode
                "{\"time\": \"2017-07-27T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}\n" +
                "{\"time\": \"2017-07-27T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"malformed\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                1
            },
            new object[] {
                // responseBytes
                "{\"time\": \"2017-07-28T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}\n" +
                "{\"time\": \"2017-07-28T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"malformed\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                1
            },
            new object[] {
                // timeTaken
                "{\"time\": \"2017-07-29T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.133\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}\n" +
                "{\"time\": \"2017-07-29T16:50:09.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"sdfsafsdf\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/system.net.primitives/index.json\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"1196\", \"userAgent\": \"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\", \"clientIp\": \"5.6.7.8\", \"clientPort\": \"24576\", \"socketIp\": \"5.6.7.8\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"malformed\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"TCP_MISS\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://nugetgallerydev.blob.core.chinacloudapi.cn:443/v3-registration5-semver1/basetestpackage/index.json\", \"originIp\": \"9.10.11.12:443\", \"originName\": \"nugetgallerydev.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}",
                1
            },
        };

        [Theory]
        [MemberData(nameof(LogsWithMalformedData))]
        public async Task SkipsLinesWithMalformedColumns(string data, int expectedOutputLines)
        {
            Mock<ILogSource> sourceMock = SetupSource(data);

            var writeSucceeded = false;
            var outputStream = new MemoryStream();
            Mock<ILogDestination> destinationMock = SetupDestination(outputStream, () => writeSucceeded = true);

            var collector = new ChinaStatsCollector(
                sourceMock.Object,
                destinationMock.Object,
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: true,
                addSourceFilenameColumn: false);

            await collector.TryProcessAsync(
                maxFileCount: 10,
                fileNameTransform: (s, _) => s,
                sourceContentType: ContentType.Text,
                destinationContentType: ContentType.Text,
                CancellationToken.None);

            string[] outputLines = null;

            outputLines = GetStreamLines(outputStream);

            Assert.True(writeSucceeded);
            Assert.NotEmpty(outputLines);
            Assert.Equal("#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1", outputLines[0]);
            Assert.True(expectedOutputLines <= outputLines.Length - 1); // excluding header
        }

        [Fact]
        public async Task DoesNotWriteHeaderIfConfigured()
        {
            const string data = "{\"time\": \"2025-12-08T06:35:36.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"fdkfghdksfljghu\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"3291\", \"userAgent\": \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\", \"clientIp\": \"1.2.3.4\", \"clientPort\": \"24576\", \"socketIp\": \"1.2.3.4\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.137\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"PRIVATE_NOSTORE\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://testorigin.blob.core.chinacloudapi.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"originIp\": \"4.3.2.1:443\", \"originName\": \"testorigin.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}";

            Mock<ILogSource> sourceMock = SetupSource(data);
            var writeSucceeded = false;
            var outputStream = new MemoryStream();
            Mock<ILogDestination> destinationMock = SetupDestination(outputStream, () => writeSucceeded = true);

            var collector = new ChinaStatsCollector(
                sourceMock.Object,
                destinationMock.Object,
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: false,
                addSourceFilenameColumn: false);

            await collector.TryProcessAsync(
                maxFileCount: 10,
                fileNameTransform: (s, _) => s,
                sourceContentType: ContentType.Text,
                destinationContentType: ContentType.Text,
                CancellationToken.None);

            string[] outputLines = null;

            outputLines = GetStreamLines(outputStream);
            Assert.True(writeSucceeded);
            Assert.Single(outputLines);
            Assert.False(outputLines[0].StartsWith("#Fields"));
        }

        [Fact]
        public async Task WritesSourceFilenameColumn()
        {
            const string data = "{\"time\": \"2025-12-08T06:35:36.0000000Z\", \"resourceId\": \"/SUBSCRIPTIONS/35f1195e-f159-4eeb-a6b6-224d0853001e/RESOURCEGROUPS/TEST-RG/PROVIDERS/MICROSOFT.CDN/PROFILES/TEST-PROFILE\", \"category\": \"FrontDoorAccessLog\", \"operationName\": \"Microsoft.Cdn/Profiles/AccessLog/Write\", \"properties\": { \"trackingReference\": \"fdkfghdksfljghu\", \"httpMethod\": \"GET\", \"httpVersion\": \"1.1.0.0\", \"requestUri\": \"https://nugetdev.azure.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"sni\": \"nugetdev.azure.cn\", \"requestBytes\": \"673\", \"responseBytes\": \"3291\", \"userAgent\": \"NuGet VS VSIX/4.7.0 (Microsoft Windows NT 10.0.17134.0, VS Enterprise/15.0)\", \"clientIp\": \"1.2.3.4\", \"clientPort\": \"24576\", \"socketIp\": \"1.2.3.4\", \"timeToFirstByte\": \"0.137\", \"timeTaken\": \"0.137\", \"requestProtocol\": \"HTTPS\", \"securityProtocol\": \"TLS 1.2\", \"rulesEngineMatchNames\": [ \"DefaultRuleset_addHstsHeader\", \"DefaultRuleset_v3dirrewrite\" ], \"httpStatusCode\": \"200\", \"edgeActionsStatusCode\": \"0\", \"roxyConnectStatusCode\": \"\", \"httpStatusDetails\": \"200\", \"pop\": \"cne\", \"cacheStatus\": \"PRIVATE_NOSTORE\", \"errorInfo\": \"NoError\", \"ErrorInfo\": \"NoError\", \"result\": \"N/A\", \"endpoint\": \"apidev-afd-ep-fgfeexdrhfb8e3de.z01.frontdoor.azure.cn\", \"routingRuleName\": \"apidev-afd-route\", \"clientJA4FingerPrint\": \"\", \"hostName\": \"nugetdev.azure.cn\", \"originUrl\": \"https://testorigin.blob.core.chinacloudapi.cn:443/v3-flatcontainer/microsoft.codeanalysis.common/1.2.2/microsoft.codeanalysis.common.1.2.2.nupkg\", \"originIp\": \"4.3.2.1:443\", \"originName\": \"testorigin.blob.core.chinacloudapi.cn:443\", \"originCryptProtocol\": \"N/A\", \"originCryptCipher\": \"N/A\", \"referer\": \"\", \"clientCountry\": \"\", \"domain\": \"nugetdev.azure.cn:443\", \"securityCipher\": \"ECDHE-RSA-AES256-GCM-SHA384\", \"securityCurves\": \"X25519:prime256v1:secp384r1\"}}";

            Mock<ILogSource> sourceMock = SetupSource(data);
            var writeSucceeded = false;
            var outputStream = new MemoryStream();
            Mock<ILogDestination> destinationMock = SetupDestination(outputStream, () => writeSucceeded = true);

            var collector = new ChinaStatsCollector(
                sourceMock.Object,
                destinationMock.Object,
                Mock.Of<ILogger<ChinaStatsCollector>>(),
                writeHeader: true,
                addSourceFilenameColumn: true);

            await collector.TryProcessAsync(
                maxFileCount: 10,
                fileNameTransform: (s, _) => s,
                sourceContentType: ContentType.Text,
                destinationContentType: ContentType.Text,
                CancellationToken.None);

            string[] outputLines = null;

            outputLines = GetStreamLines(outputStream);
            Assert.True(writeSucceeded);
            Assert.Equal(2, outputLines.Length);
            Assert.EndsWith("sourceFilename", outputLines[0]);
            Assert.EndsWith("log1", outputLines[1]);
        }

        private static Mock<ILogDestination> SetupDestination(MemoryStream outputStream, Action onSuccess)
        {
            var destinationMock = new Mock<ILogDestination>();
            destinationMock
                .Setup(d => d.TryWriteAsync(It.IsAny<Stream>(), It.IsAny<Action<Stream, Stream>>(), It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream inputStream, Action<Stream, Stream> writeAction, string destinationFileName, ContentType destinationContentType, CancellationToken token) =>
                {
                    try
                    {
                        writeAction(inputStream, outputStream);
                        onSuccess();
                    }
                    catch (Exception ex)
                    {
                        return new AsyncOperationResult(false, ex);
                    }
                    return new AsyncOperationResult(true, null);
                });
            return destinationMock;
        }

        private static Mock<ILogSource> SetupSource(string content)
        {
            var sourceUri = new Uri("https://example.com/log1");

            var sourceMock = new Mock<ILogSource>();
            sourceMock
                .Setup(s => s.GetFilesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync((IEnumerable<Uri>)new List<Uri> { sourceUri });

            sourceMock
                .Setup(s => s.TakeLockAsync(sourceUri, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureBlobLockResult(new BlobClient(sourceUri), new BlobProperties(), true, "foo", CancellationToken.None));

            sourceMock
                .Setup(s => s.OpenReadAsync(sourceUri, It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
            return sourceMock;
        }

        private static string[] GetStreamLines(MemoryStream outputStream)
        {
            string[] outputLines;
            // need to reopen closed stream
            var outputBuffer = outputStream.ToArray();
            outputStream = new MemoryStream(outputBuffer);
            using (var streamReader = new StreamReader(outputStream))
            {
                outputLines = streamReader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return outputLines;
        }
    }
}
