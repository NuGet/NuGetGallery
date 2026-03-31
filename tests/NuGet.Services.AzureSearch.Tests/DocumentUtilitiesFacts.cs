// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class DocumentUtilitiesFacts
    {
        public class Base64UrlEncode
        {
            [Theory]
            [InlineData (new byte[] { 0x00 }, "AA2")]
            [InlineData (new byte[] { 0x00, 0x01 }, "AAE1")]
            [InlineData (new byte[] { 0x00, 0x01, 0x02 }, "AAEC0")]
            [InlineData (new byte[] { 0xFF }, "_w2")]
            [InlineData (new byte[] { 0xFF, 0xFE }, "__41")]
            [InlineData (new byte[] { 0xFF, 0xFE, 0xFD }, "__790")]
            [InlineData (new byte[] { 0x3E, 0x3F }, "Pj81")]
            [InlineData (new byte[] { 0xFB, 0xEF, 0xBE }, "---+0")]
            [InlineData (new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, "AQIDBAUG0")]
            [InlineData (new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }, "SGVsbG81")]
            public void ProducesExpectedOutput(byte[] input, string expected)
            {
                var actual = DocumentUtilities.Base64UrlEncode(input);
                Assert.Equal(expected, actual);
            }
        }

        public class GetHijackDocumentKey
        {
            [Theory]
            [InlineData("NuGet.Versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("nuget.versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("NUGET.VERSIONING", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("_", "1.0.0", "1_0_0-Xy8xLjAuMA2")]
            [InlineData("foo-bar", "1.0.0", "foo-bar_1_0_0-Zm9vLWJhci8xLjAuMA2")]
            [InlineData("İzmir", "1.0.0", "zmir_1_0_0-xLB6bWlyLzEuMC4w0")]
            [InlineData("İİzmir", "1.0.0", "zmir_1_0_0-xLDEsHptaXIvMS4wLjA1")]
            [InlineData("zİİmir", "1.0.0", "z__mir_1_0_0-esSwxLBtaXIvMS4wLjA1")]
            [InlineData("zmirİ", "1.0.0", "zmir__1_0_0-em1pcsSwLzEuMC4w0")]
            [InlineData("zmirİİ", "1.0.0", "zmir___1_0_0-em1pcsSwxLAvMS4wLjA1")]
            [InlineData("惡", "1.0.0", "1_0_0-5oOhLzEuMC4w0")]
            [InlineData("jQuery", "1.0.0-alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-Alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA.1", "jquery_1_0_0-alpha_1-anF1ZXJ5LzEuMC4wLWFscGhhLjE1")]
            public void EncodesHijackDocumentKey(string id, string version, string expected)
            {
                var actual = DocumentUtilities.GetHijackDocumentKey(id, version);

                Assert.Equal(expected, actual);
            }
        }

        public class GetSearchDocumentKey
        {
            [Theory]
            [InlineData("NuGet.Versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("nuget.versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("NUGET.VERSIONING", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("_", "Xw2")]
            [InlineData("foo-bar", "foo-bar-Zm9vLWJhcg2")]
            [InlineData("İzmir", "zmir-xLB6bWly0")]
            [InlineData("İİzmir", "zmir-xLDEsHptaXI1")]
            [InlineData("zİİmir", "z__mir-esSwxLBtaXI1")]
            [InlineData("zmirİ", "zmir_-em1pcsSw0")]
            [InlineData("zmirİİ", "zmir__-em1pcsSwxLA1")]
            [InlineData("惡", "5oOh0")]
            public void EncodesSearchDocumentKey(string id, string expected)
            {
                foreach (var searchFilters in Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>())
                {
                    var actual = DocumentUtilities.GetSearchDocumentKey(id, searchFilters);

                    Assert.Equal(expected + "-" + searchFilters, actual);
                }
            }
        }
    }
}
