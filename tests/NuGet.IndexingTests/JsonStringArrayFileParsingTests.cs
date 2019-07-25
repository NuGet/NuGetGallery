// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests
{
    public class JsonStringArrayFileParserTest
    {
        [Theory]
        [InlineData("[]", new string[0])]
        [InlineData("['test']", new string[] { "test" })]
        [InlineData("['test', 'test']", new string[] { "test" })]
        [InlineData("['test', 'test123']", new string[] { "test", "test123" })]
        [InlineData("['test', 'Test123', 'test123', 'tEst123']", new string[] { "test", "test123" })]
        public void ParsesProperInput(string input, string[] expected)
        {
            var actual = Parse(input);

            Assert.Equal(expected.Length, actual.Count);
            Assert.True(expected.All(actual.Contains));
        }

        [Theory]
        [InlineData("{'test':'hi'}")]
        [InlineData("['test', 'test123'")]
        [InlineData("['test', {'test':'hi'}]")]
        public void ThrowsOnInvalidInput(string input)
        {
            Assert.ThrowsAny<Exception>(() => Parse(input));
        }

        private HashSet<string> Parse(string input)
        {
            return JsonStringArrayFileParser.Parse(new JsonTextReader(new StringReader(input)));
        }
    }
}
