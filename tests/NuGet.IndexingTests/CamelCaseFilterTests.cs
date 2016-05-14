// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using NuGet.Indexing;
using NuGet.IndexingTests.TestSupport;
using Xunit;

namespace NuGet.IndexingTests
{
    public class CamelCaseFilterTests
    {
        [Theory]
        [MemberData(nameof(SplitterSamples))]
        public void SplitterTests(string input, string[] expected)
        {
            var splits = CamelCaseFilter.CamelCaseSplit(input);

            Assert.Equal(expected, splits);
        }

        public static IEnumerable<object[]> SplitterSamples
        {
            get
            {
                yield return new object[]
                {
                    "DotNetZipFoo",
                    new[] { "Dot", "Net", "Zip", "Foo" },
                };

                // shingle depth two with three terms
                yield return new object[]
                {
                    "DotNetZip",
                    new[] { "Dot", "Net", "Zip"},
                };

                // two terms
                yield return new object[]
                {
                    "DotNet",
                    new[] { "Dot", "Net" },
                };

                // one term
                yield return new object[]
                {
                    "Dot",
                    new[] { "Dot" },
                };

                // empty query
                yield return new object[]
                {
                    string.Empty,
                    new object[0],
                };

                // maintain case
                yield return new object[]
                {
                    "DOT",
                    new[] { "DOT" },
                };

                // camel case transition is only when the characters go from lowercase to uppercase
                yield return new object[]
                {
                    "DOTNet",
                    new[] { "DOTNet" },
                };

                // one character camel case
                yield return new object[]
                {
                    "DotN",
                    new[] { "Dot", "N" },
                };

                // Split on number
                yield return new object[]
                {
                    "Mvc5",
                    new[] { "Mvc", "5" },
                };

                // Split on two numbers
                yield return new object[]
                {
                    "Log4Net",
                    new[] { "Log", "4", "Net" },
                };

                // Split on two numbers and one at the end
                yield return new object[]
                {
                    "Log4Net5",
                    new[] { "Log", "4", "Net", "5"  },
                };

                // Split on more than one digit number
                yield return new object[]
                {
                    "Log44Net",
                    new[] { "Log", "44", "Net" },
                };
            }
        }

        [Theory]
        [MemberData(nameof(TokenizingReturnsExpectedTermAndOffsetsData))]
        public void TokenizingReturnsExpectedTermAndOffsets(string text, TokenAttributes[] expected)
        {
            // arrange
            var tokenStream = new StandardTokenizer(Version.LUCENE_30, new StringReader(text));
            var filter = new CamelCaseFilter(tokenStream);

            // act
            var actual = filter.Tokenize().ToArray();

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> TokenizingReturnsExpectedTermAndOffsetsData
        {
            get
            {
                // shingle depth two with four terms
                yield return new object[]
                {
                    "DotNetZipFoo",
                    new[]
                    {
                        new TokenAttributes("DotNetZipFoo", 0, 12, 1),
                        new TokenAttributes("Dot", 0, 3, 0),
                        new TokenAttributes("DotNet", 0, 6, 0),
                        new TokenAttributes("Net", 3, 6, 1),
                        new TokenAttributes("NetZip", 3, 9, 0),
                        new TokenAttributes("Zip", 6, 9, 1),
                        new TokenAttributes("ZipFoo", 6, 12, 0),
                        new TokenAttributes("Foo", 9, 12, 1)
                    }
                };

                // shingle depth two with three terms
                yield return new object[]
                {
                    "DotNetZip",
                    new[]
                    {
                        new TokenAttributes("DotNetZip", 0, 9, 1),
                        new TokenAttributes("Dot", 0, 3, 0),
                        new TokenAttributes("DotNet", 0, 6, 0),
                        new TokenAttributes("Net", 3, 6, 1),
                        new TokenAttributes("NetZip", 3, 9, 0),
                        new TokenAttributes("Zip", 6, 9, 1),
                    }
                };

                // two terms
                yield return new object[]
                {
                    "DotNet",
                    new[]
                    {
                        new TokenAttributes("DotNet", 0, 6, 1),
                        new TokenAttributes("Dot", 0, 3, 0),
                        new TokenAttributes("Net", 3, 6, 1)
                    }
                };

                // one term
                yield return new object[]
                {
                    "Dot",
                    new[]
                    {
                        new TokenAttributes("Dot", 0, 3, 1)
                    }
                };

                // empty query
                yield return new object[]
                {
                    string.Empty,
                    new object[0]
                };

                // maintain case
                yield return new object[]
                {
                    "DOT",
                    new[]
                    {
                        new TokenAttributes("DOT", 0, 3, 1)
                    }
                };

                // camel case transition is only when the characters go from lowercase to uppercase
                yield return new object[]
                {
                    "DOTNet",
                    new[]
                    {
                        new TokenAttributes("DOTNet", 0, 6, 1)
                    }
                };

                // one character camel case
                yield return new object[]
                {
                    "DotN",
                    new[]
                    {
                        new TokenAttributes("DotN", 0, 4, 1),
                        new TokenAttributes("Dot", 0, 3, 0),
                        new TokenAttributes("N", 3, 4, 1)
                    }
                };

                yield return new object[]
                {
                    "Mvc5",
                    new[]
                    {
                        new TokenAttributes("Mvc5", 0, 4, 1),
                        new TokenAttributes("Mvc", 0, 3 ,0 ),
                    }
                };

                yield return new object[]
                {
                    "Log4Net",
                    new[]
                    {
                        new TokenAttributes("Log4Net", 0, 7, 1),
                        new TokenAttributes("Log", 0, 3, 0),
                        new TokenAttributes("Log4", 0, 4, 0),
                        new TokenAttributes("4Net", 3, 7, 0),
                        new TokenAttributes("Net", 4, 7, 1),
                    }
                };
            }
        }
    }
}
