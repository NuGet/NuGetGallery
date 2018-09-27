// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Specialized;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class UploadHelperFacts
    {
        public class TheGetUploadTracingKeyMethod
        {
            private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

            [Fact]
            public void ReturnsEmptyGuidForMissingHeader()
            {
                var headers = new NameValueCollection();

                var result = UploadHelper.GetUploadTracingKey(headers);

                Assert.Equal(EmptyGuid, result);
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData("not-a-guid")]
            [InlineData("00000000-0000-0000-0000-000000000000")]
            [InlineData(" 00000000-0000-0000-0000-000000000000 ")]
            public void ReturnsEmptyGuidForInvalidOrEmptyGuid(string value)
            {
                var headers = new NameValueCollection();
                headers["upload-id"] = value;

                var result = UploadHelper.GetUploadTracingKey(headers);

                Assert.Equal(EmptyGuid, result);
            }

            [Theory]
            [InlineData("3bcf7d12-eb0a-46c9-98a8-c160801e8134")]
            [InlineData("3bcf7d12eb0a46c998a8c160801e8134")]
            [InlineData("3BCF7D12-EB0A-46C9-98A8-C160801E8134")]
            [InlineData(" 3bcf7d12-eb0a-46c9-98a8-c160801e8134 ")]
            public void ReturnsGuidForValidGuid(string value)
            {
                var headers = new NameValueCollection();
                headers["upload-id"] = value;

                var result = UploadHelper.GetUploadTracingKey(headers);

                Assert.Equal("3bcf7d12-eb0a-46c9-98a8-c160801e8134", result);
            }
        }
    }
}
