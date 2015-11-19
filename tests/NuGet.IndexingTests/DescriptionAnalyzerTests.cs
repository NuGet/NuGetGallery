// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class DescriptionAnalyzerTests
    {
        [Theory]
        [MemberData(nameof(TokenizerLowercasesCamelCasesAndRemovesStopWordsInputData))]
        public void TokenizerLowercasesCamelCasesAndRemovesStopWordsInput(string text, TokenAttributes[] expected)
        {
            // arrange, act
            var actual = new DescriptionAnalyzer().Tokenize(text);

            // assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(TokenizerRemovesCorrectStopWordsData))]
        public void TokenizerRemovesCorrectStopWords(string stopWord)
        {
            // arrange, act
            var text = $"stop {stopWord} word";
            var actual = new DescriptionAnalyzer().Tokenize(text);
            var expected = new[]
            {
                new TokenAttributes("stop", 0, 4, 1),
                new TokenAttributes("word", 6 + stopWord.Length, 10 + stopWord.Length, 2)
            };

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizerLowercasesCamelCasesAndRemovesStopWordsInputData
        {
            get
            {
                // split by DotTokenizer
                yield return new object[]
                {
                    "Split sentence.",
                    new[]
                    {
                        new TokenAttributes("split", 0, 5, 1),
                        new TokenAttributes("sentence", 6, 14, 1)
                    }
                };

                // split on camel case
                yield return new object[]
                {
                    "DotNet",
                    new[]
                    {
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("dot", 0, 3, 0),
                        new TokenAttributes("net", 3, 6, 1)
                    }
                };

                // lower case 
                yield return new object[]
                {
                    "D",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1, 1)
                    }
                };

                // remove stop words
                yield return new object[]
                {
                    "This is a sentence full of stop words.",
                    new[]
                    {
                        new TokenAttributes("sentence", 10, 18, 4),
                        new TokenAttributes("full", 19, 23, 1),
                        new TokenAttributes("stop", 27, 31, 2),
                        new TokenAttributes("words", 32, 37, 1)
                    }
                };

                // combined
                yield return new object[]
                {
                    "This is a half-baked sentence is describing DotNet.",
                    new[]
                    {
                        new TokenAttributes("half", 10, 14, 4),
                        new TokenAttributes("baked", 15, 20, 1),
                        new TokenAttributes("sentence", 21, 29, 1),
                        new TokenAttributes("describing", 33, 43, 2),
                        new TokenAttributes("dotnet", 44, 50, 1),
                        new TokenAttributes("dot", 44, 47, 0),
                        new TokenAttributes("net", 47, 50, 1)
                    }
                };
            }
        }

        public static IEnumerable<object[]> TokenizerRemovesCorrectStopWordsData
        {
            get
            {
                yield return new object[] { "a" };
                yield return new object[] { "an" };
                yield return new object[] { "and" };
                yield return new object[] { "are" };
                yield return new object[] { "as" };
                yield return new object[] { "at" };
                yield return new object[] { "be" };
                yield return new object[] { "but" };
                yield return new object[] { "by" };
                yield return new object[] { "for" };
                yield return new object[] { "i" };
                yield return new object[] { "if" };
                yield return new object[] { "in" };
                yield return new object[] { "into" };
                yield return new object[] { "is" };
                yield return new object[] { "it" };
                yield return new object[] { "its" };
                yield return new object[] { "no" };
                yield return new object[] { "not" };
                yield return new object[] { "of" };
                yield return new object[] { "on" };
                yield return new object[] { "or" };
                yield return new object[] { "s" };
                yield return new object[] { "such" };
                yield return new object[] { "that" };
                yield return new object[] { "the" };
                yield return new object[] { "their" };
                yield return new object[] { "then" };
                yield return new object[] { "there" };
                yield return new object[] { "these" };
                yield return new object[] { "they" };
                yield return new object[] { "this" };
                yield return new object[] { "to" };
                yield return new object[] { "was" };
                yield return new object[] { "we" };
                yield return new object[] { "will" };
                yield return new object[] { "with" };
            }
        }
    }
}
