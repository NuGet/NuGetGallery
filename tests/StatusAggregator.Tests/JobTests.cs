// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace StatusAggregator.Tests
{
    public class JobTests
    {
        public class TheGetCertificateFromConfigurationMethod
        {
            /// <remarks>
            /// This is a self-signed certificate that was created solely for the purpose of this test.
            /// It has no password.
            /// </remarks>
            private const string _certificateBase64 =
                "MIIDGDCCAgCgAwIBAgIQS97pQXcKf4NH38AoyLpy1DANBgkqhkiG9w0BAQsFADAfMR0wGwYDVQQDDBRz" +
                "dGF0dXNhZ2dyZWdhdG9ydGVzdDAeFw0xOTA3MjYxNzU4MzVaFw0yMDA3MjYxODE4MzVaMB8xHTAbBgNV" +
                "BAMMFHN0YXR1c2FnZ3JlZ2F0b3J0ZXN0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1jvc" +
                "9y+24qAs9Wjihoa9xJfwewkGwZROMvL57DvIhWnNyLiUqRviRR8yMfc674EWC3pCYKPgGA2aKIVHZBcR" +
                "0QIB4CPoB3IuiVGuKXI3c1R6vJV9QgfodoGT4liXCdA01jgqbWYBnrXICpSGjwm21rCmXEA6InI0py0p" +
                "1k8Kq1phijONmQHpa58MANhE6SRb8/knv6kLNJwC9ZjSMzsdAvyrxBHRidX1hMduy8Y6lVnkC243fvEP" +
                "bB3dICkbmSap1E9WSonR53pEt0HwJdCIzKX/HnG32VlWG2Jud9fNoACw/zgx7+yNmQrOD1b0SXQTfz0e" +
                "/8NH/Su/Fv27eafb6QIDAQABo1AwTjAOBgNVHQ8BAf8EBAMCBaAwHQYDVR0lBBYwFAYIKwYBBQUHAwIG" +
                "CCsGAQUFBwMBMB0GA1UdDgQWBBQRctibnTaB9NgpH3wNvM3Y83KO5jANBgkqhkiG9w0BAQsFAAOCAQEA" +
                "XkdXOpS1r68Vnl2ADOStc7ct6CZI3srcRUV8ryXJILg/jvj7x9AAGwASkkgfpvG2mz9sEs0DMOIRJ4/3" +
                "6fdqR6ud/19xfHEHKfbAF+SNBKyutvJ0tmf+q9jd2GI7bYBbBhW7wrTYOpe2XcY8hix8q08jyrNC+Dja" +
                "VSh6n+HxFYyA6oDfRg+6nXPsZYdgHjevYFah/cmWU7F0k+E3n2V2rUfBdXNrWxiu/jsUi4p8gpC6HAST" +
                "jcLaO6IhsmJU3d5YRUduOeyqYq5yksjr+3jWphsESzC2w1V+H91V2WpXGWRLB3LU9BgBeC0CTRetrarx" +
                "RwAe7G+JUatDppyP+8jKZA==";

            private readonly ILogger _logger = NullLogger.Instance;

            [Fact]
            public void HandlesJsonCerts()
            {
                var password = "password";
                var certWithPasswordBytes = 
                    new X509Certificate2(Convert.FromBase64String(_certificateBase64))
                    .Export(X509ContentType.Cert, password);

                var certWithPasswordBase64 = Convert.ToBase64String(certWithPasswordBytes);
                var data = new CertificateData
                {
                    Data = certWithPasswordBase64,
                    Password = password
                };

                var cert = Job.GetCertificateFromConfiguration(JsonConvert.SerializeObject(data), _logger);

                var base64 = Convert.ToBase64String(cert.RawData);
                Assert.Equal(certWithPasswordBase64, base64);
            }

            [Fact]
            public void HandlesBase64Certs()
            {
                var cert = Job.GetCertificateFromConfiguration(_certificateBase64, _logger);

                var base64 = Convert.ToBase64String(cert.RawData);
                Assert.Equal(_certificateBase64, base64);
            }

            private class CertificateData
            {
                public string Data { get; set; }
                public string Password { get; set; }
            }
        }
    }
}
