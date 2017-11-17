// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text;
using NuGetGallery.Infrastructure.Authentication;
using Xunit;

namespace NuGetGallery.Infrastructure
{
    public class Base32EncoderTests
    {
        private Random _random = new Random(0);

        [Fact]
        public void WhenDataIsNullEncodeThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() => Base32Encoder.Encode(data: null));
        }

        [Fact]
        public void WhenDataIsNullDecodeThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() => Base32Encoder.Decode(base32String: null));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("abc")]
        [InlineData("SEMTXET5UU6UZDD4AMK57TR46I==")]
        [InlineData("mjwgc===")]
        public void WhenDataIsNotLegalBase32DecodeThrows(string input)
        {
            Assert.Throws<InvalidOperationException>(() => Base32Encoder.Decode(base32String: input));
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("NBSWY3DP", "hello")]
        [InlineData("EA======", " ")]
        [InlineData("EB2GQ2LTEBUXGIDBEB3GK4TZEBWG63THEBZXI4TJNZTSA53JORUCA3DPORZSA33GEBQWCYLBMFQWCYLBMEQGS3RAORUGKIDFNZSCAYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYLBMFQWCYI=",
                    " this is a very long string with lots of aaaaaaaaaa in the end aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void WhenValidBase32StringProvideDecodeSucceeds(string base32String, string decodedString)
        {
            Assert.Equal(decodedString, Encoding.ASCII.GetString(base32String.FromBase32String()));
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
            var decoded = encoded.FromBase32String();

            // Assert           
            Assert.True(byteArr.SequenceEqual(decoded));
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
            var decoded = withPadding.FromBase32String();

            // Assert
            Assert.True(byteArr.SequenceEqual(decoded));
        }
    }
}
