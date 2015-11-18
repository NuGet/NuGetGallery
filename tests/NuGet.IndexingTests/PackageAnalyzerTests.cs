// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class PackageAnalyzerTests
    {
        [Theory, MemberData("TheoryData")]
        public void AddsCorrectFieldAnalyzers(string field, string text, TokenAttributes[] expected)
        {
            // ARRANGE
            var analyzer = new PackageAnalyzer();

            // ACT
            var tokenStream = analyzer.TokenStream(field, new StringReader(text));
            var tokenAttributes = tokenStream.GetTokenAttributes().ToArray();

            // ASSERT
            Assert.Equal(expected, tokenAttributes);
        }

        public static IEnumerable<object[]> TheoryData
        {
            get
            {
                yield return new object[]
                {
                    "Id",
                    "AaBbCc",
                    new[]
                    {
                        new TokenAttributes("aabbcc", 0, 6)
                    }
                };

                yield return new object[]
                {
                    "IdAutocomplete",
                    "AaB",
                    new[]
                    {
                        new TokenAttributes("a", 0, 1, 1),
                        new TokenAttributes("aa", 0, 2, 1),
                        new TokenAttributes("aab", 0, 3, 1),
                        new TokenAttributes("a", 0, 1, 1),
                        new TokenAttributes("aa", 0, 2, 1),
                        new TokenAttributes("b", 2, 3, 1)
                    }
                };

                yield return new object[]
                {
                    "TokenizedId",
                    "AaBb",
                    new[]
                    {
                        new TokenAttributes("aabb", 0, 4, 1),
                        new TokenAttributes("aa", 0, 2, 0),
                        new TokenAttributes("bb", 2, 4, 1)
                    }
                };

                yield return new object[]
                {
                    "Version",
                    "01.002.0003",
                    new[]
                    {
                        new TokenAttributes("1.2.3", 0, 11)
                    }
                };

                yield return GetDescriptionTestCase("Title");
                yield return GetDescriptionTestCase("Description");
                yield return GetDescriptionTestCase("Summary");
                yield return GetDescriptionTestCase("Authors");
                
                yield return new object[]
                {
                    "Owner",
                    "AAA Bbb",
                    new[]
                    {
                        new TokenAttributes("aaa bbb", 0, 7)
                    }
                };

                yield return new object[]
                {
                    "Tags",
                    "AAA Bbb ccc",
                    new[]
                    {
                        new TokenAttributes("aaa", 0, 3, null),
                        new TokenAttributes("bbb", 4, 7, null),
                        new TokenAttributes("ccc", 8, 11, null),
                    }
                };
            }
        }

        private static object[] GetDescriptionTestCase(string field)
        {
            return new object[]
            {
                field,
                "There is a package called AaBb.",
                new[]
                {
                    new TokenAttributes("package", 11, 18, 4),
                    new TokenAttributes("called", 19, 25, 1),
                    new TokenAttributes("aabb", 26, 30, 1),
                    new TokenAttributes("aa", 26, 28, 0),
                    new TokenAttributes("bb", 28, 30, 1)
                }
            };
        }
    }
}
