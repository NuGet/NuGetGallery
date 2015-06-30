// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.SearchTests
{
    public class LanguageNameSearchTest : NuGetFluentTest
    {
        public LanguageNameSearchTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Validate that the language names C# and C++ return distinct and meaningful results.")]
        [Priority(2)]
        public void LanguageNameSearch()
        {
            // Go to the front page.
            I.Open(UrlHelper.BaseUrl);

            // Search for C++ and C#, verify search results don't cross-contaminate.
            I.Enter("C++").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('C#')");

            I.Enter("C#").In("#searchBoxInput");
            I.Click("#searchBoxSubmit");
            I.Expect.Count(0).Of("h1:contains('C++')");
        }
    }
}
