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
        private const int MaxSize = 30;

        [Fact]
        public async Task ReadNullStreamThrowException()
        {
            Stream stream = null;
            using (var memoryStream = new MemoryStream())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => stream.GetTruncatedStreamWithMaxSizeAsync(MaxSize));
                Assert.Equal(nameof(stream), exception.ParamName);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(int.MaxValue)]
        public async Task ReadStreamWithInvalidMaxSizeThrowException(int maxSize)
        {
            using (var stream = new MemoryStream())
            {
                using (var memoryStream = new MemoryStream())
                {
                    var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => stream.GetTruncatedStreamWithMaxSizeAsync(maxSize));
                    Assert.Equal(nameof(maxSize), exception.ParamName);
                }
            }
        }

        [Fact]
        public async Task ReadStreamWithLessThanMaxSize()
        {
            var stringContent = GenerateRandomString(MaxSize - 1);
            var expectedStringContent = stringContent;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedStringContent)))
            {
                using (var memoryStream = new MemoryStream())
                {
                    var truncatedMemoryStream = await stream.GetTruncatedStreamWithMaxSizeAsync(MaxSize);
                    Assert.False(truncatedMemoryStream.IsTruncated);
                    Assert.Equal(MaxSize - 1, (int)truncatedMemoryStream.Stream.Length);
                    Assert.Equal(expectedStringContent, Encoding.UTF8.GetString(truncatedMemoryStream.Stream.ToArray()));
                }
            }
        }

        [Fact]
        public async Task ReadStreamWithEqualToMaxSize()
        {
            var stringContent = GenerateRandomString(MaxSize);
            var expectedStringContent = stringContent;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(stringContent)))
            {
                using (var memoryStream = new MemoryStream())
                {
                    var truncatedMemoryStream = await stream.GetTruncatedStreamWithMaxSizeAsync(MaxSize);
                    Assert.False(truncatedMemoryStream.IsTruncated);
                    Assert.Equal(MaxSize, (int)truncatedMemoryStream.Stream.Length);
                    Assert.Equal(expectedStringContent, Encoding.UTF8.GetString(truncatedMemoryStream.Stream.ToArray()));
                }
            }
        }

        [Fact]
        public async Task ReadStreamWithLargerThanMaxSize()
        {
            var stringContent = GenerateRandomString(MaxSize + 1);
            var expectedStringContent = stringContent.Substring(0, MaxSize);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(stringContent)))
            {
                using (var memoryStream = new MemoryStream())
                {
                    var truncatedMemoryStream = await stream.GetTruncatedStreamWithMaxSizeAsync(MaxSize);
                    Assert.True(truncatedMemoryStream.IsTruncated);
                    Assert.Equal(MaxSize, (int)truncatedMemoryStream.Stream.Length);
                    Assert.Equal(expectedStringContent, Encoding.UTF8.GetString(truncatedMemoryStream.Stream.ToArray()));
                }
            }
        }

        private string GenerateRandomString(int length)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringBuilder = new StringBuilder();
            var random = new Random();
            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(chars[random.Next(chars.Length)]);
            }

            return stringBuilder.ToString();
        }
    }
}