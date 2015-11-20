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
    public class DotTokenizerTests
    {
        [Theory]
        [MemberData("SplitsTextIntoTokensOnCorrectCharactersData")]
        public void SplitsTextIntoTokensOnCorrectCharacters(char seperator)
        {
            // arrange
            var text = string.Format("Dot{0}NET", seperator);
            var tokenizer = new DotTokenizer(new StringReader(text));
            var expected = new[] { new TokenAttributes("Dot", 0, 3), new TokenAttributes("NET", 4, 7) };

            // act
            var actual = tokenizer.Tokenize().ToArray();

            // assert
            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> SplitsTextIntoTokensOnCorrectCharactersData
        {
            get
            {
                yield return new object[] { ' ' };
                yield return new object[] { '\t' };
                yield return new object[] { '\r' };
                yield return new object[] { '\n' };
                yield return new object[] { '.' };
                yield return new object[] { '-' };
                yield return new object[] { ',' };
                yield return new object[] { ';' };
                yield return new object[] { ':' };
                yield return new object[] { '\'' };
                yield return new object[] { '*' };
                yield return new object[] { '#' };
                yield return new object[] { '!' };
                yield return new object[] { '~' };
                yield return new object[] { '+' };
                yield return new object[] { '-' };
                yield return new object[] { '(' };
                yield return new object[] { ')' };
                yield return new object[] { '[' };
                yield return new object[] { ']' };
                yield return new object[] { '{' };
                yield return new object[] { '}' };
            }
        }
        
    }
}
