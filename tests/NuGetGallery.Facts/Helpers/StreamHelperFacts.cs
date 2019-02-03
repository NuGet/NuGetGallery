﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class StreamHelperFacts
    {
        [Fact]
        public async Task ReadNullStreamThrowException()
        {
            Stream stream = null;
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => StreamHelper.ReadMaxAsync(stream, maxSize: 100));
            Assert.Equal(nameof(stream), exception.ParamName);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public async Task ReadStreamWithInvalidMaxSizeThrowException(int maxSize)
        {
            using (var stream = new MemoryStream())
            {
                var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => StreamHelper.ReadMaxAsync(stream, maxSize));
                Assert.Equal(nameof(maxSize), exception.ParamName);
            }
        }

        [Theory]
        [InlineData("test content")]
        [InlineData("测试内容")]
        public async Task CheckContentWhenReadValidStream(string inputContent)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(inputContent)))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, maxSize: 100);
                Assert.Equal(inputContent, content);
            }
        }

        [Theory]
        [InlineData(1, 0, -1)]
        [InlineData(1, 0, 1)]
        [InlineData(1, 0, 0)]
        [InlineData(1, -1, -1)]
        [InlineData(1, -1, 1)]
        [InlineData(1, -1, 0)]
        [InlineData(2, 0, -1)]
        [InlineData(2, 0, 1)]
        [InlineData(2, 0, 0)]
        public async Task CheckContentSizeWithDifferentParameters(int maxSizeMultiplier, int maxSizeOffset, int bytesContentSizeOffset)
        {
            long maxSize = StreamHelper.BufferSize * maxSizeMultiplier + maxSizeOffset;
            Assert.True(maxSize < long.MaxValue);
            var bytesContentSize = maxSize + bytesContentSizeOffset;
            Assert.True(bytesContentSize < int.MaxValue);
            var bytesContent = new byte[bytesContentSize];
            var expectedContentSize = Math.Min(bytesContentSize, maxSize);

            using (var stream = new MemoryStream(bytesContent))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, maxSize);
                Assert.Equal(expectedContentSize, Encoding.UTF8.GetBytes(content).Length);
            }
        }
    }
}