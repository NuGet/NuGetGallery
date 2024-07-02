// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class TextHelperFacts
    {
        public static IEnumerable<object[]> IsTextFile_Input => new object[][]
        {
            new object[] { new byte[] { 0, 1, 2, 3 }, false },
            new object[] { new byte[] { 10, 13, 12 }, true },
            new object[] { Encoding.UTF8.GetBytes("Sample license test"), true},
            new object[] { Encoding.UTF8.GetBytes("тест тест"), true},
            new object[] { Encoding.UTF8.GetBytes("test\tlicense. Some line breaks\r\nSome non-latin characters: тест тест\n some random \r line \n breaks.\n\f\nAnd a form feed."), true},
            new object[] { Encoding.UTF32.GetBytes("UTF-32 text is considered binary"), false},
            new object[] { Array.Empty<byte>(), true },
        };

        // any characters with code <32 except line break and tab characters should be rejected
        public static IEnumerable<object[]> IsTextFile_IndividualBytes =>
            from @byte in Enumerable.Range(0, 256)
            select new object[] { new byte[1] { (byte)@byte }, @byte == '\r' || @byte == '\n' || @byte == '\t' || @byte == '\f' || @byte >= 32 };

        [Theory]
        [MemberData(nameof(IsTextFile_Input))]
        [MemberData(nameof(IsTextFile_IndividualBytes))]
        public async Task ClassifiesStreamCorrectly(byte[] data, bool expectedToBeText)
        {
            bool isText;
            using (var stream = new MemoryStream(data))
            {
                isText = await TextHelper.LooksLikeUtf8TextStreamAsync(stream);
            }

            Assert.Equal(expectedToBeText, isText);
        }

        [Fact]
        public async Task HandlesLargeInput()
        {
            // had to make it a separate test case, because the argument below makes VS test explorer
            // very unhappy and never finish test discovery
            var largeInput = Encoding.UTF8.GetBytes(string.Join(
                    ", ",
                    Enumerable
                        .Range(0, 1000000)
                        .Select(x => "abcdefghijklmnopqrstuvwxyz")));

            bool isText;
            using (var stream = new MemoryStream(largeInput))
            {
                isText = await TextHelper.LooksLikeUtf8TextStreamAsync(stream);
            }

            Assert.True(isText);
        }

        [Theory]
        [MemberData(nameof(IsTextFile_IndividualBytes))]
        public void ClassifiesBytesCorrectly(byte[] input, bool expectedToBeText)
        {
            bool isText = TextHelper.IsUtf8TextByte(input[0]);
            Assert.Equal(expectedToBeText, isText);
        }

        [Fact]
        public async Task ThrowsWhenStreamIsNull()
        {
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => TextHelper.LooksLikeUtf8TextStreamAsync(null, 10));
            Assert.Equal("stream", ex.ParamName);
        }

        [Fact]
        public async Task ThrowsWhenStreamIsNotReadable()
        {
            var writeOnlyStream = new WriteOnlyStream();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => TextHelper.LooksLikeUtf8TextStreamAsync(writeOnlyStream, 10));
            Assert.Equal("stream", ex.ParamName);
        }

        [Fact]
        public async Task ThrowsWhenBufferSizeIsNegative()
        {
            var writeOnlyStream = new MemoryStream();
            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TextHelper.LooksLikeUtf8TextStreamAsync(writeOnlyStream, -1));
            Assert.Equal("bufferSize", ex.ParamName);
        }

        private class WriteOnlyStream : MemoryStream
        {
            public override bool CanRead => false;
        }
    }

    public class LargeTestGenerator : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator() => Generator().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<object[]> Generator()
        {
            yield return new object[]
            {
                Encoding.UTF8.GetBytes(string.Join(
                    ", ",
                    Enumerable
                        .Range(0, 1000000)
                        .Select(x => "abcdefghijklmnopqrstuvwxyz"))),
                true
            };
        }
    }
}
