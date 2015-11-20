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
        [MemberData("TokenizingReturnsExpectedTermAndOffsetsData")]
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

                // for now, we do not split on numbers in CamelCaseFilter. We may revisit this in the future.
                yield return new object[]
                {
                    "Dot1Net2ZIP3",
                    new[]
                    {
                        new TokenAttributes("Dot1Net2ZIP3", 0, 12, 1)
                    }
                };
            }
        }
    }
}
