// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;

namespace NuGetGallery
{
    public static class RegexEx
    {
        // This timeout must be short enough to prevent runaway regular expressions,
        // but long enough to prevent reliability issues across all our regular expressions.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Creates a new instance of the <see cref="Regex"/> class with a default timeout configured
        /// for the pattern matching method to attempt a match.
        /// </summary>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combiantion of the enumeration values that modify the expression.</param>
        /// <returns>A regular expression instance that can be used to match inputs.</returns>
        public static Regex CreateWithTimeout(string pattern, RegexOptions options)
        {
            return new Regex(pattern, options, Timeout);
        }

        /// <summary>
        /// In a specific input string, replaces all substrings that match a specified regular expression.
        /// Throws a <see cref="RegexMatchTimeoutException"/> if the timeout is reached.
        /// </summary>
        /// <param name="input">The string to search for matches.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="evaluator">The handler to replace matches.</param>
        /// <param name="options">A bitwise combination that provide options for matching.</param>
        /// <returns>A new string with the matches replaced.</returns>
        /// <exception cref="RegexMatchTimeoutException">Thrown if the matches exceed the default timeout.</exception>
        public static string ReplaceWithTimeout(
            string input,
            string pattern,
            string replacement,
            RegexOptions options)
        {
            return Regex.Replace(input, pattern, replacement, options, Timeout);
        }

        /// <summary>
        /// In a specific input string, replaces all substrings that match a specified regular expression.
        /// </summary>
        /// <param name="input">The string to search for matches.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="evaluator">The handler to replace matches.</param>
        /// <param name="options">A bitwise combination that provide options for matching.</param>
        /// <returns>A new string with the matches replaced, or the original string if the matches timeout.</returns>
        public static string ReplaceWithTimeoutOrOriginal(
            string input,
            string pattern,
            MatchEvaluator evaluator,
            RegexOptions options)
        {
            try
            {
                return Regex.Replace(input, pattern, evaluator, options, Timeout);
            }
            catch (RegexMatchTimeoutException)
            {
                return input;
            }
        }

        /// <summary>
        /// Searches the input string for the first occurrence of the specified regular expression,
        /// using the specified matching options and the default time-out interval.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>An object that contains information about the match, or <c>null</c> if and only if the match timed out.</returns>
        public static Match MatchWithTimeoutOrNull(
            string input,
            string pattern,
            RegexOptions options)
        {
            try
            {
                return Regex.Match(input, pattern, options, Timeout);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        /// <summary>
        /// Searches the input string for all occurrence of the specified regular expression,
        /// using the specified matching options and the default time-out interval.
        /// </summary>
        /// <param name="input">The string to search for a match.</param>
        /// <param name="pattern">The regular expression pattern to match.</param>
        /// <param name="options">A bitwise combination of the enumeration values that provide options for matching.</param>
        /// <returns>
        /// A collection of the matches found by the search.
        /// If no matches are found, the method returns an empty collection.
        /// If and only if the matches timeout, returns <c>null</c>.</returns>
        public static MatchCollection MatchesWithTimeoutOrNull(
            string input,
            string pattern,
            RegexOptions options)
        {
            try
            {
                return Regex.Matches(input, pattern, options, Timeout);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }
    }
}