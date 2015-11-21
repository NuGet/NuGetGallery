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
        [Theory]
        [MemberData("AddsCorrectFieldAnalyzersData")]
        public void AddsCorrectFieldAnalyzers(string field, string text, TokenAttributes[] expected)
        {
            // arrange
            var analyzer = new PackageAnalyzer();

            // act
            var tokenStream = analyzer.TokenStream(field, new StringReader(text));
            var actual = tokenStream.Tokenize().ToArray();

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> AddsCorrectFieldAnalyzersData
        {
            get
            {
                yield return new object[]
                {
                    "Id",
                    "DotNetZip",
                    new[]
                    {
                        new TokenAttributes("dotnetzip", 0, 9)
                    }
                };
                
                yield return new object[]
                {
                    "IdAutocomplete",
                    "DotNet",
                    new[]
                    {
                        new TokenAttributes("d", 0, 1, 1),
                        new TokenAttributes("do", 0, 2, 1),
                        new TokenAttributes("dot", 0, 3, 1),
                        new TokenAttributes("dotn", 0, 4, 1),
                        new TokenAttributes("dotne", 0, 5, 1),
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("d", 0, 1, 1),
                        new TokenAttributes("do", 0, 2, 1),
                        new TokenAttributes("dot", 0, 3, 1),
                        new TokenAttributes("n", 3, 4, 1),
                        new TokenAttributes("ne", 3, 5, 1),
                        new TokenAttributes("net", 3, 6, 1),

                    }
                };
                
                yield return new object[]
                {
                    "TokenizedId",
                    "DotNet",
                    new[]
                    {
                        new TokenAttributes("dotnet", 0, 6, 1),
                        new TokenAttributes("dot", 0, 3, 0),
                        new TokenAttributes("net", 3, 6, 1)
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
                    "Microsoft",
                    new[]
                    {
                        new TokenAttributes("microsoft", 0, 9)
                    }
                };

                yield return new object[]
                {
                    "Tags",
                    "DOT Net zip",
                    new[]
                    {
                        new TokenAttributes("dot", 0, 3, null),
                        new TokenAttributes("net", 4, 7, null),
                        new TokenAttributes("zip", 8, 11, null),
                    }
                };
            }
        }

        private static object[] GetDescriptionTestCase(string field)
        {
            return new object[]
            {
                field,
                "There is a package called DotNetZip.",
                new[]
                {
                    new TokenAttributes("package", 11, 18, 4),
                    new TokenAttributes("called", 19, 25, 1),
                    new TokenAttributes("dotnetzip", 26, 35, 1),
                    new TokenAttributes("dot", 26, 29, 0),
                    new TokenAttributes("dotnet", 26, 32, 0),
                    new TokenAttributes("net", 29, 32, 1),
                    new TokenAttributes("netzip", 29, 35, 0),
                    new TokenAttributes("zip", 32, 35, 1)
                }
            };
        }
    }
}
