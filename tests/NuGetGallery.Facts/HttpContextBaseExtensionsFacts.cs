// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class HttpContextBaseExtensionsFacts
    {
        [Theory]
        [InlineData("NuGet Command Line/4.1.0 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line/4.1.0")]
        [InlineData("", "")]
        [InlineData("     ", "")]
        [InlineData(null, "")]
        [InlineData("NuGet VS PowerShell Console/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet VS PowerShell Console/1.2.3")]
        [InlineData("Package-Installer/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "Package-Installer/1.2.3")]
        [InlineData("NuGet Command Line/1.2.3 (Microsoft Windows NT 6.2.9200.0)", "NuGet Command Line/1.2.3")]
        [InlineData("Paket", "Paket")]
        [InlineData("curl/7.21.0 (x86_64-pc-linux-gnu) libcurl/7.21.0 (OpenSSL/0.9.8o) zlib/1.2.3.4 libidn/1.18", "curl/7.21.0")]
        [InlineData("Java/1.7.0_51", "Java/1.7.0_51")]
        [InlineData("dotPeek/102.0.20150521.130901 (Microsoft Windows NT 6.3.9600.0; NuGet/2.8.60318.667; Wave/2.0.0; dotPeek/1.4.20150521.130901)", "dotPeek/102.0.20150521.130901")]
        public void EnrichesTelemetryWithClientInfo(string userAgent, string expectedClientInfo)
        {
            // Arrange
            var headers = new NameValueCollection
            {
                { ServicesConstants.UserAgentHeaderName, userAgent }
            };

            var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
            httpRequest.SetupGet(r => r.Headers).Returns(headers);

            var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
            httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

            // Act
            var result = httpContext.Object.GetClientInformation();

            // Assert
            Assert.Equal(expectedClientInfo, result);
        }
    }
}
