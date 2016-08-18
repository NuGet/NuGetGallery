// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;
using Xunit;

namespace NuGetGallery
{
    /// <summary>
    /// The regex checks that the password is atleast 8 characters, one uppercase letter, one lowercase letter, and a digit.
    /// </summary>
    public class PasswordValidationRegexTests
    {
        [Theory]
        [InlineData("aA1aaaaa")]
        [InlineData("abcdefg$0B")]
        [InlineData("****1bB***")]
        public void Accepts(string password)
        {
            var match = new Regex(RegisterViewModel.PasswordValidationRegex).IsMatch(password);
            Assert.True(match);
        }

        [Theory]
        [InlineData("aa")]
        [InlineData("aaAAaaAAaaAA")]
        [InlineData("12345678")]
        public void DoesNotAccept(string password)
        {
            var match = new Regex(RegisterViewModel.PasswordValidationRegex).IsMatch(password);
            Assert.False(match);
        }
    }
}
