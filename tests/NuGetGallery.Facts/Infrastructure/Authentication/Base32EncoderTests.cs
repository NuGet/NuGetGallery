// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class Base32EncoderTests
    {
        private Random _random = new Random(0);

        // Maps string to its base32 encoding
        public static IEnumerable<object[]> ValidBase32 => new List<object[]>
        {
            new object[] { "", "" },
            new object[] { "f", "MY======" },
            new object[] { "fo", "MZXQ====" },
            new object[] { "foo", "MZXW6===" },
            new object[] { "foob", "MZXW6YQ=" },
            new object[] { "fooba", "MZXW6YTB" },
            new object[] { "foobar", "MZXW6YTBOI======" },
        };

        public static IEnumerable<object[]> InvalidBase32 => new List<object[]>
        {
            new object[] { "a" },
            new object[] { "abc" },
            new object[] { "SEMTXET5UU6UZDD4AMK57TR46I==" },
            new object[] { "mjwgc===" },
            new object[] { "GEYq====" },
        };

        [Fact]
        public void EncodeThrowsNull()
        {
            Assert.Throws<NullReferenceException>(() => Base32Encoder.Encode(data: null));
        }

        [Fact]
        public void DecodeThrowsNull()
        {
            Assert.Throws<NullReferenceException>(() => Base32Encoder.Decode(base32String: null));
        }

        [Fact]
        public void TryDecodeRejectsNull()
        {
            string input = null;

            Assert.False(input.TryDecodeBase32String(out var result));
        }

        [Theory]
        [MemberData(nameof(InvalidBase32))]
        public void DecodeThrowsInvalidArgument(string input)
        {
            Assert.Throws<ArgumentException>(() => Base32Encoder.Decode(base32String: input));
        }

        [Theory]
        [MemberData(nameof(InvalidBase32))]
        public void TryParseRejectsInvalidBase32(string input)
        {
            Assert.False(input.TryDecodeBase32String(out var result));
        }

        [Theory]
        [MemberData(nameof(ValidBase32))]
        public void DecodesBase32(string decodedString, string base32String)
        {
            Assert.Equal(decodedString, Encoding.ASCII.GetString(base32String.FromBase32String()));
        }

        [Theory]
        [MemberData(nameof(ValidBase32))]
        public void TryDecodesBase32(string decodedString, string base32String)
        {
            var success = base32String.TryDecodeBase32String(out var result);

            Assert.True(success);
            Assert.Equal(decodedString, Encoding.ASCII.GetString(result));
        }

        [Theory]
        [MemberData(nameof(ValidBase32))]
        public void EncodesBase32(string input, string base32String)
        {
            Assert.Equal(base32String, Base32Encoder.ToBase32String(Encoding.ASCII.GetBytes(input)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("hello")]
        [InlineData("1234456")]
        [InlineData("זה יוניקוד")]
        public void WhenDataIsEncodedItCanBeDecoded(string input)
        {
            // Arrange
            var byteArr = Encoding.Unicode.GetBytes(input);

            // Act
            var encoded = byteArr.ToBase32String();
            var decoded1 = encoded.FromBase32String();
            var success = encoded.TryDecodeBase32String(out var decoded2);

            // Assert           
            Assert.True(success);
            Assert.True(byteArr.SequenceEqual(decoded1));
            Assert.True(byteArr.SequenceEqual(decoded2));
        }

        [Theory]
        [InlineData(1)] // Padding length: 6
        [InlineData(2)] // Padding length: 4
        [InlineData(3)] // Padding length: 3
        [InlineData(4)] // Padding length: 1
        [InlineData(5)] // Padding length: 0
        public void WhenPaddingIsRemovedItCanBeAppendedBack(int byteArrayLength)
        {
            // Arrange 
            byte[] byteArr = new byte[byteArrayLength];
            _random.NextBytes(byteArr);

            var encoded = byteArr.ToBase32String();
            var noPadding = encoded.RemoveBase32Padding();

            // Act
            var withPadding = noPadding.AppendBase32Padding();
            var decoded1 = withPadding.FromBase32String();
            var success = withPadding.TryDecodeBase32String(out var decoded2);

            // Assert
            Assert.True(success);
            Assert.True(byteArr.SequenceEqual(decoded1));
            Assert.True(byteArr.SequenceEqual(decoded2));
        }
    }
}
