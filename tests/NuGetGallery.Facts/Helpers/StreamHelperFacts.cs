﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Globalization;

namespace NuGetGallery.Helpers
{
    public class StreamHelperFacts
    {
        [Theory]
        [InlineData("test content")]
        [InlineData("测试内容")]
        public async Task ReadValidStreamToString(string inputContent)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(inputContent)))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, 1000);

                Assert.Equal(content, inputContent);
            }
        }

        [Fact]
        public async Task ReadInvalidStreamToString()
        {
            var maxSize = 1000;
            using (var stream = new MemoryStream(new byte[maxSize + 1]))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StreamHelper.ReadMaxAsync(stream, maxSize));

                Assert.Contains(exception.Message, string.Format(CultureInfo.CurrentCulture, Strings.StreamMaxLengthExceeded, maxSize));
            }
        }
    }
}