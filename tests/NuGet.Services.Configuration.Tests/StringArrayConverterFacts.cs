// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using Xunit;

namespace NuGet.Services.Configuration.Tests
{
    public class StringArrayConverterFacts
    {
        [Fact]
        public void CanConvertFromString()
        {
            var target = new StringArrayConverter();

            Assert.True(target.CanConvertFrom(typeof(string)));
        }

        [Fact]
        public void SplitsStringBySemicolon()
        {
            var target = new StringArrayConverter();

            var output = target.ConvertFrom("foo;bar  ;  baz");

            var array = Assert.IsType<string[]>(output);
            Assert.Equal(new[] { "foo", "bar  ", "  baz" }, array);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo bar")]
        [InlineData("foo|bar")]
        [InlineData("foo,bar")]
        [InlineData("   ")]
        public void ReturnsSingleStringWhenThereIsNoDelimeter(string input)
        {
            var target = new StringArrayConverter();

            var output = target.ConvertFrom(input);

            var array = Assert.IsType<string[]>(output);
            Assert.Equal(new[] { input }, array);
        }

        [Fact]
        public void ReturnsEmptyArrayWithEmpty()
        {
            var target = new StringArrayConverter();

            var output = target.ConvertFrom(string.Empty);

            var array = Assert.IsType<string[]>(output);
            Assert.Empty(array);
        }
    }
}
