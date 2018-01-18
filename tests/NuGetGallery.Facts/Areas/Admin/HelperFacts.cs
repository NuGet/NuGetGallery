// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGetGallery.Areas.Admin
{
    public class HelperFacts
    {
        public class TheParseQueryToLinesMethod
        {
            [Theory]
            [MemberData(nameof(Queries))]
            public void ParsedToLines(string query, string[] lineArray)
            {
                // Arrange
                var expected = lineArray.ToList();

                // Act
                var actual = Helpers.ParseQueryToLines(query);

                // Assert
                Assert.Equal(expected, actual);
            }

            public static IEnumerable<object[]> Queries
            {
                get
                {
                    yield return new object[] { "NuGet.Versioning", new[] { "NuGet.Versioning" } };
                    yield return new object[] { "\tNuGet.Versioning  ", new[] { "NuGet.Versioning" } };
                    yield return new object[] { "NuGet.Versioning\n", new[] { "NuGet.Versioning" } };
                    yield return new object[] { "NuGet.Versioning\r\t\n  \t NuGet.Frameworks  ", new[] { "NuGet.Versioning", "NuGet.Frameworks" } };
                    yield return new object[] { "a  \t  b\n  c    d  ", new[] { "a b", "c d" } };
                    yield return new object[] { "\r\n\n\n", new string[0] };
                }
            }
        }
    }
}
