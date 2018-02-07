﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.EditableMetadata
{
    public class EditAndSearchTest : NuGetFluentTest
    {
        private readonly PackageCreationHelper _packageCreationHelper;

        public EditAndSearchTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
            _packageCreationHelper = new PackageCreationHelper(testOutputHelper);
        }

        [Fact]
        [Description("Provide sanity verification of search index rebuilding on the live site.")]
        [Priority(2)]
        public async Task EditAndSearch()
        {
            // Use the same package name, but force the version to be unique.
            string packageName = "NuGetGallery.FunctionalTests.Fluent.EditAndSearch";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string newPackageLocation = await _packageCreationHelper.CreatePackage(packageName, version);

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountEmail, EnvironmentSettings.TestAccountPassword);

            // Navigate to the upload page.
            I.UploadPackageUsingUI(newPackageLocation);

            // Edit the package.
            I.Click("#Edit_VersionTitleButton");
            string newTitle = string.Format("This title is accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newTitle).In("#Edit_VersionTitle");
            I.Click("#verifyUploadSubmit");

            // Re-load and validate that the edit has been applied.
            // The edit can be applied anytime within ten mins. to be considered successful.  This tries 20 times with a 30 sec. timeout.
            bool applied = false;
            for (int i = 0; i < 20; i++)
            {
                I.Enter(newTitle).In("#searchBoxInput");
                I.Click("#searchBoxSubmit");
                // Starting API V3, we have removed the search sort order
                //I.Select("Recent").From("#sortOrder");
                try
                {
                    I.Expect.Count(2).Of("h1:contains('" + newTitle + "')");
                    applied = true;
                }
                catch
                {
                    // We expect an exception after 30 seconds if the edit hasn't been applied yet.
                }
            }

            Assert.True(applied, "The edit doesn't appear to have been applied after ten minutes.");
        }
    }
}
