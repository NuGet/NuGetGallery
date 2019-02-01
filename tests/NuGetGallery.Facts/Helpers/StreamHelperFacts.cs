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
        private const int MaxSize = 100;

        [Fact]
        public async Task ReadNullStreamThrowException()
        {
            Stream stream = null;
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => StreamHelper.ReadMaxAsync(stream, MaxSize));
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
        public async Task ReadValidStream(string inputContent)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(inputContent)))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, MaxSize);
                Assert.Equal(inputContent, content);
            }
        }

        [Fact]
        public async Task ReadStreamWithLessThanMaxSize()
        {
            var byteContent = new byte[MaxSize - 1];
            using (var stream = new MemoryStream(byteContent))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, MaxSize);
                Assert.Equal(byteContent.Length, Encoding.UTF8.GetBytes(content).Length);
            }
        }

        [Fact]
        public async Task ReadStreamWithMaxSize()
        {
            var byteContent = new byte[MaxSize];
            using (var stream = new MemoryStream(byteContent))
            {
                var content = await StreamHelper.ReadMaxAsync(stream, MaxSize);
                Assert.Equal(byteContent.Length, Encoding.UTF8.GetBytes(content).Length);
            }
        }

        [Fact]
        public async Task ReadStreamWithLargerThanMaxSizeThrowException()
        {
            using (var stream = new MemoryStream(new byte[MaxSize + 1]))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StreamHelper.ReadMaxAsync(stream, MaxSize));
                Assert.Contains(exception.Message, string.Format(CultureInfo.CurrentCulture, Strings.StreamMaxLengthExceeded, MaxSize));
            }
        }
    }
}