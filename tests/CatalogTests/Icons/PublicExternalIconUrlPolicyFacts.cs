// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Metadata.Catalog.Icons;
using Xunit;

namespace CatalogTests.Icons
{
    public class PublicExternalIconUrlPolicyFacts
    {
        private readonly PublicExternalIconUrlPolicy _target;

        public PublicExternalIconUrlPolicyFacts()
        {
            _target = new PublicExternalIconUrlPolicy(Mock.Of<ILogger<PublicExternalIconUrlPolicy>>());
        }

        [Theory]
        [InlineData("ftp://example.com/icon.png")]
        [InlineData("file:///C:/icon.png")]
        [InlineData("javascript:alert(1)")]
        public async Task RejectsNonHttpSchemes(string url)
        {
            Assert.False(await _target.IsAllowedAsync(new Uri(url), CancellationToken.None));
        }

        [Theory]
        [InlineData("http://example.com:8080/icon.png")]
        [InlineData("https://example.com:8443/icon.png")]
        [InlineData("http://127.0.0.1:1234/icon.png")]
        public async Task RejectsNonDefaultPorts(string url)
        {
            Assert.False(await _target.IsAllowedAsync(new Uri(url), CancellationToken.None));
        }

        [Theory]
        // loopback
        [InlineData("http://127.0.0.1/icon.png")]
        [InlineData("http://127.255.255.254/icon.png")]
        [InlineData("http://[::1]/icon.png")]
        // private RFC1918
        [InlineData("http://10.0.0.1/icon.png")]
        [InlineData("http://10.255.255.255/icon.png")]
        [InlineData("http://172.16.0.1/icon.png")]
        [InlineData("http://172.31.255.254/icon.png")]
        [InlineData("http://192.168.1.1/icon.png")]
        // link-local
        [InlineData("http://169.254.169.254/latest/meta-data/")]
        // carrier-grade NAT
        [InlineData("http://100.64.0.1/icon.png")]
        // 0.0.0.0/8
        [InlineData("http://0.0.0.0/icon.png")]
        // multicast
        [InlineData("http://224.0.0.1/icon.png")]
        // reserved
        [InlineData("http://240.0.0.1/icon.png")]
        [InlineData("http://255.255.255.255/icon.png")]
        // IPv6 unique local
        [InlineData("http://[fc00::1]/icon.png")]
        [InlineData("http://[fd00::1]/icon.png")]
        // IPv6 link-local
        [InlineData("http://[fe80::1]/icon.png")]
        // IPv4-mapped IPv6 to private
        [InlineData("http://[::ffff:10.0.0.1]/icon.png")]
        [InlineData("http://[::ffff:127.0.0.1]/icon.png")]
        public async Task RejectsRestrictedHostLiterals(string url)
        {
            Assert.False(await _target.IsAllowedAsync(new Uri(url), CancellationToken.None));
        }

        [Theory]
        [InlineData("http://8.8.8.8/icon.png")]
        [InlineData("https://1.1.1.1/icon.png")]
        public async Task AllowsPublicIpLiterals(string url)
        {
            Assert.True(await _target.IsAllowedAsync(new Uri(url), CancellationToken.None));
        }

        [Fact]
        public async Task RejectsHostnamesThatFailDnsResolution()
        {
            var url = new Uri("https://this-host-should-not-resolve.invalid/icon.png");
            Assert.False(await _target.IsAllowedAsync(url, CancellationToken.None));
        }

        [Fact]
        public async Task RejectsLocalhostHostname()
        {
            // "localhost" resolves to a loopback address.
            var url = new Uri("http://localhost/icon.png");
            Assert.False(await _target.IsAllowedAsync(url, CancellationToken.None));
        }

        [Fact]
        public async Task RejectsNullUri()
        {
            Assert.False(await _target.IsAllowedAsync(null, CancellationToken.None));
        }

        [Theory]
        [InlineData("0.0.0.0", true)]
        [InlineData("10.1.2.3", true)]
        [InlineData("100.64.0.1", true)]
        [InlineData("100.127.255.255", true)]
        [InlineData("100.128.0.1", false)] // outside CGN range
        [InlineData("127.0.0.1", true)]
        [InlineData("169.254.1.1", true)]
        [InlineData("172.15.0.1", false)]
        [InlineData("172.16.0.1", true)]
        [InlineData("172.31.255.255", true)]
        [InlineData("172.32.0.1", false)]
        [InlineData("192.168.0.1", true)]
        [InlineData("198.18.0.1", true)]
        [InlineData("198.19.255.255", true)]
        [InlineData("198.20.0.1", false)]
        [InlineData("224.0.0.1", true)]
        [InlineData("239.255.255.255", true)]
        [InlineData("240.0.0.1", true)]
        [InlineData("255.255.255.255", true)]
        [InlineData("8.8.8.8", false)]
        [InlineData("1.1.1.1", false)]
        [InlineData("203.0.113.1", true)] // TEST-NET-3
        [InlineData("198.51.100.1", true)] // TEST-NET-2
        [InlineData("192.0.2.1", true)] // TEST-NET-1
        public void IsRestrictedAddressClassifiesIPv4Correctly(string ipString, bool expected)
        {
            var ip = IPAddress.Parse(ipString);
            Assert.Equal(expected, PublicExternalIconUrlPolicy.IsRestrictedAddress(ip));
        }

        [Theory]
        [InlineData("::1", true)]
        [InlineData("fe80::1", true)]
        [InlineData("fc00::1", true)]
        [InlineData("fd00::1", true)]
        [InlineData("ff02::1", true)]
        [InlineData("2001:db8::1", true)]
        [InlineData("2606:4700:4700::1111", false)] // Cloudflare DNS
        public void IsRestrictedAddressClassifiesIPv6Correctly(string ipString, bool expected)
        {
            var ip = IPAddress.Parse(ipString);
            Assert.Equal(expected, PublicExternalIconUrlPolicy.IsRestrictedAddress(ip));
        }
    }
}
