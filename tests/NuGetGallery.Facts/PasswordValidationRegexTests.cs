// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    /// <summary>
    /// The regex checks that the password is at least 8 characters, one uppercase letter, one lowercase letter, and a digit.
    /// </summary>
    public class PasswordValidationRegexTests : TestContainer
    {
        private readonly string _defaultPasswordRegex;

        public PasswordValidationRegexTests()
        {
            var configuration = Get<ConfigurationService>();
            _defaultPasswordRegex = configuration.Current.UserPasswordRegex;
        }

        [Theory]
        [InlineData("aA1aaaaa")]
        [InlineData("abcdefg$0B")]
        [InlineData("****1bB***")]
        public void Accepts(string password)
        {
            
            var match = new Regex(_defaultPasswordRegex).IsMatch(password);
            Assert.True(match);
        }

        [Theory]
        [InlineData("v")] // Single letter
        [InlineData("V")] // Single upper case letter
        [InlineData("8")] // Single number
        [InlineData("89984214214")] // Just numbers
        [InlineData("%*`~&*()%#@$!@<>?\"")] // Special characters
        [InlineData("aaAAaaAAaaAA")] // No digit
        [InlineData("12345678a")] // No upperscase letter
        [InlineData("12345678A")] // No lowercase letter
        [InlineData("1aA")] // Too short
        public void DoesNotAccept(string password)
        {
            var match = new Regex(_defaultPasswordRegex).IsMatch(password);
            Assert.False(match);
        }
    }
}
