// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.SearchTests
{
    public class TagSearchTest : NuGetFluentTest
    {
        public TagSearchTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Verify parsing of functional tags from package metadata.")]
        [Priority(2)]
        public async Task TagSearch()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.TagSearchTest";
            string version = "1.0.0";
            string tagString = ";This,is a,;test,,package, created ;by ,the  NuGet;;;team.";

            if (CheckForPackageExistence)
            {
                await UploadPackageIfNecessary(packageName, version, null, null, tagString, "This is a test package created by the NuGet team.");
            }

            // Go to the package page.
            I.Open(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
            string[] tags = tagString.Split(" ,;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (string tag in tags)
            {
                I.Expect.Text(tag).In("a[title='Search for " + tag + "']");
            }
        }
    }
}
