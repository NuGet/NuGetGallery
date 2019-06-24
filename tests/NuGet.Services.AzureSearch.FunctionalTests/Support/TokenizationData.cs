// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public static class TokenizationData
    {
        public static readonly IEnumerable<object[]> LowercasesTokens = ToMemberData(new Dictionary<string, string[]>
        {
            { "hello", new[] { "hello"} },
            { "Hello", new[] { "hello" } },
            { "𠈓", new[] { "𠈓" } },
        });

        public static readonly IEnumerable<object[]> SplitsTokensOnSpecialCharactersAndLowercases = ToMemberData(new Dictionary<string, string[]>
        {
            { "Foo.Bar", new[] { "foo", "bar" } },
            { "Foo-Bar", new[] { "foo", "bar" } },
            { "Foo,Bar", new[] { "foo", "bar" } },
            { "Foo;Bar", new[] { "foo", "bar" } },
            { "Foo:Bar", new[] { "foo", "bar" } },
            { "Foo'Bar", new[] { "foo", "bar" } },
            { "Foo*Bar", new[] { "foo", "bar" } },
            { "Foo#Bar", new[] { "foo", "bar" } },
            { "Foo!Bar", new[] { "foo", "bar" } },
            { "Foo~Bar", new[] { "foo", "bar" } },
            { "Foo+Bar", new[] { "foo", "bar" } },
            { "Foo(Bar", new[] { "foo", "bar" } },
            { "Foo)Bar", new[] { "foo", "bar" } },
            { "Foo[Bar", new[] { "foo", "bar" } },
            { "Foo]Bar", new[] { "foo", "bar" } },
            { "Foo{Bar", new[] { "foo", "bar" } },
            { "Foo}Bar", new[] { "foo", "bar" } },
            { "Foo_Bar", new[] { "foo", "bar" } },
            { "Foo_𠈓_bar", new[] { "foo", "𠈓", "bar" } },
        });

        public static readonly IEnumerable<object[]> LowercasesAndAddsTokensOnCasingAndNonAlphaNumeric = ToMemberData(new Dictionary<string, string[]>
        {
            { "HelloWorld", new[] { "helloworld", "hello", "world" } },
            { "foo2bar", new[] { "foo2bar", "foo", "2", "bar" } },
            { "HTML", new[] { "html"} },
            { "HTMLThing", new[] { "htmlthing" } },
            { "HTMLThingA", new[] { "htmlthinga", "htmlthing", "a" } },
            { "HelloWorld𠈓Foo", new[] { "helloworld", "hello", "world𠈓foo" } },
        });

        public static readonly IEnumerable<object[]> AddsTokensOnNonAlphaNumericAndRemovesStopWords = ToMemberData(new Dictionary<string, string[]>
        {
            { "a", new string[0] },
            {
                "a an and are as at be but by hello for if in into is no not " +
                "of on or such that the",
                new[]
                {
                    "hello"
                }
            },
            {
                "Once upon a time, there was a little test-case!",
                new[]
                {
                    "once", "upon", "time", "little", "test", "case"
                }
            }
        });

        private static List<object[]> ToMemberData(Dictionary<string, string[]> data)
        {
            return data.Select(d => new object[] { d.Key, d.Value }).ToList();
        }
    }
}
