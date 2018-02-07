﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests.Fluent.EditableMetadata
{
    public class EditPackageTest : NuGetFluentTest
    {
        public EditPackageTest(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact]
        [Description("Edit every possible metadata field of an uploaded package.")]
        [Priority(2)]
        public async Task EditPackage()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.EditPackageTest";
            string version = "1.0.0";

            await UploadPackageIfNecessary(packageName, version);

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountEmail, EnvironmentSettings.TestAccountPassword);

            // Navigate to the package's edit page.
            I.Open(string.Format(UrlHelper.EditPageUrl, packageName, version));
            I.Expect.Url(x => x.AbsoluteUri.Contains("nuget"));

            // Edit the package.
            string newTitle = string.Format("This title is accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newTitle).In("#Edit_VersionTitle");

            string newDescription = string.Format("This description is accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newDescription).In("#Edit_Description");

            string newSummary = string.Format("This summary is accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newSummary).In("#Edit_Summary");

            string newIconUrl = string.Format("http://microsoft.com/IconUrl/{0}", DateTime.Now.ToString("FFFFFFF"));
            I.Enter(newIconUrl).In("#Edit_IconUrl");

            string newHomePageUrl = string.Format("http://microsoft.com/HomePageUrl/{0}", DateTime.Now.ToString("FFFFFFF"));
            I.Enter(newHomePageUrl).In("#Edit_ProjectUrl");

            string newAuthors = string.Format("These authors are accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newAuthors).In("#Edit_Authors");

            string newCopyright = string.Format("Copyright {0}.", DateTime.Now.ToString("F"));
            I.Enter(newCopyright).In("#Edit_Copyright");

            string newTags = string.Format("These tags are accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newTags).In("#Edit_Tags");

            string newReleaseNotes = string.Format("These release notes are accurate as of {0}.", DateTime.Now.ToString("F"));
            I.Enter(newReleaseNotes).In("#Edit_ReleaseNotes");

            I.Click("input[value=Save]");

            // Validate that the edit is queued.
            I.Expect.Url(UrlHelper.BaseUrl + @"packages/" + packageName + "/" + version);
            string expectedDescription = @"p:contains('" + newDescription + "')";
            string unexpectedSummary = @"contains('" + newSummary + "')";
            string unexpectedIconUrl = "img[src='" + newIconUrl + "']";
            string defaultIconUrl = UrlHelper.BaseUrl + "Content/Images/packageDefaultIcon.png";
            string expectedIconUrl = "img[src='" + defaultIconUrl + "']";
            string expectedHomePageUrl = "a[href='" + newHomePageUrl + "']";
            string unexpectedCopyright = @"contains('" + newCopyright + "')";
            string expectedReleaseNotes = @"p:contains('" + newReleaseNotes + "')";
            string editPending = @"p:contains('An edit is pending for this package version.')";

            I.Expect.Count(1).Of(expectedDescription);
            I.Expect.Count(0).Of(unexpectedSummary); // Summary is not present on the package page.
            I.Expect.Count(0).Of(unexpectedIconUrl);
            I.Expect.Count(1).Of(expectedIconUrl);
            I.Expect.Count(1).Of(expectedHomePageUrl);
            I.Expect.Text(newAuthors).In("p[class=authors]");
            I.Expect.Count(0).Of(unexpectedCopyright); // Copyright is not present on the package page.
            I.Expect.Count(1).Of(expectedReleaseNotes);
            string[] tags = newTags.Split(" ,;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (string tag in tags)
            {
                I.Expect.Text(tag).In("a[title='Search for " + tag + "']");
            }

            // Validate the pending edit message.
            I.Expect.Count(1).Of(editPending);

            // Re-load and validate that the edit has been applied.
            // The edit can be applied anytime within 5 mins. to be considered successful.  This tries 10 times with a 30 sec. timeout.
            bool applied = false;
            for (int i = 0; i < 10; i++)
            {
                I.Open(UrlHelper.BaseUrl + @"Packages/" + packageName + "/" + version);
                I.Expect.Count(1).Of(expectedDescription);
                try
                {
                    I.Expect.Count(0).Of(editPending);
                    applied = true;
                }
                catch
                {
                    // We expect an exception if the edit hasn't been applied yet.
                }
            }
            Assert.True(applied, "The edit doesn't appear to have been applied after five minutes.");
        }
    }
}
