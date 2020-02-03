// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;

namespace NuGetGallery.Helpers
{
    public static class RegexEx
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        public static string TryReplaceWithTimeout(
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

        public static Match MatchWithTimeout(
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
    }
}