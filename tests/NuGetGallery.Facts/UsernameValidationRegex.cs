// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace NuGetGallery
{
    public class UsernameValidationRegex
    {
        private const int TestCharSetSize = 256;

        [Theory]
        [MemberData(nameof(GetAllowedShortUsername))]
        public void AllowedUsernames(string username)
        {
            var match = new Regex(GalleryConstants.UsernameValidationRegex).IsMatch(username);
            Assert.True(match);
        }

        [Theory]
        [MemberData(nameof(GetNotAllowedSuffixPrefixCharacters))]
        public void NotAllowedUsernamesPrefix(char incorrectPrefixChar)
        {
            string username = new string(new char[] { incorrectPrefixChar, 'a' });
            var match = new Regex(GalleryConstants.UsernameValidationRegex).IsMatch(username);
            Assert.False(match);
        }

        [Theory]
        [MemberData(nameof(GetNotAllowedSuffixPrefixCharacters))]
        public void NotAllowedUsernamesSuffix(char incorrectSuffixChar)
        {
            string username = new string(new char[] { 'a', incorrectSuffixChar });
            var match = new Regex(GalleryConstants.UsernameValidationRegex).IsMatch(username);
            Assert.False(match);
        }

        [Theory]
        [MemberData(nameof(GetNotAllowedMiddleCharacters))]
        public void NotAllowedUsernamesMiddle(char incorrectMiddleChar)
        {
            string username = new string(new char[] { 'a', incorrectMiddleChar, 'b' });
            var match = new Regex(GalleryConstants.UsernameValidationRegex).IsMatch(username);
            Assert.False(match);
        }

        public static IEnumerable<object[]> GetNotAllowedSuffixPrefixCharacters()
        {
            return Enumerable.Range(0, TestCharSetSize)
                      .Select(i => (char)i)
                      .Where(c => !GetAllowedSuffixPrefixCharacters().Contains(c))
                      .Select(c => new object[] { c });
        }

        public static IEnumerable<object[]> GetNotAllowedMiddleCharacters()
        {
            return Enumerable.Range(0, TestCharSetSize)
                      .Select(i => (char)i)
                      .Where(c => !GetAllowedMiddleCharacters().Contains(c))
                      .Select(c => new object[] { c }); ;
        }

        public static IEnumerable<object[]> GetAllowedShortUsername()
        {
            char[] shortAllowedPrefixSuffixList = new char[] { 'a', 'Z', '1' };
            char[] shortAllowedMiddleCharList = new char[] { '.', '_', '-' };

            foreach (var prefix in shortAllowedPrefixSuffixList)
            {
                foreach (var middle in shortAllowedMiddleCharList)
                {
                    foreach (var suffix in shortAllowedPrefixSuffixList)
                    {
                        var v = new string(new char[] { prefix, middle, suffix });
                        yield return new object[] { new string(new char[] { prefix, middle, suffix }) };
                    }
                }
            }
        }

        public static IEnumerable<char> GetAllowedSuffixPrefixCharacters()
        {
            foreach (var index in Enumerable.Range('a', 'z' - 'a' + 1))
            {
                yield return (char)index;
            }
            foreach (var index in Enumerable.Range('A', 'Z' - 'A' + 1))
            {
                yield return (char)index;
            }
            foreach (var index in Enumerable.Range(0, 10))
            {
                yield return (char)('0' + index);
            }
        }

        public static IEnumerable<char> GetAllowedMiddleCharacters()
        {
            foreach (var allowedPrefixOrSuffix in GetAllowedSuffixPrefixCharacters())
            {
                yield return allowedPrefixOrSuffix;
            }
            foreach (var otherAllowed in new char[] {'.', '_', '-'})
            {
                yield return otherAllowed;
            }
            
        }
    }
}
