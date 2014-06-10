using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.FunctionTests.Helpers;
using System;

namespace NuGetGallery.FunctionalTests.Fluent
{
    [TestClass]
    public class EditAndSearchTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Provide sanity verification of search index rebuilding on the live site.")]
        [Priority(2)]
        public void EditAndSearch()
        {
            // Use the same package name, but force the version to be unique.
            string packageName = "NuGetGallery.FunctionalTests.Fluent.EditAndSearch";
            string ticks = DateTime.Now.Ticks.ToString();
            string version = new System.Version(ticks.Substring(0, 6) + "." + ticks.Substring(6, 6) + "." + ticks.Substring(12, 6)).ToString();
            string newPackageLocation = PackageCreationHelper.CreatePackage(packageName, version);

            // Log on using the test account.
            I.LogOn(EnvironmentSettings.TestAccountName, EnvironmentSettings.TestAccountPassword);

            // Navigate to the upload page. 
            I.UploadPackageUsingUI(newPackageLocation);

            // Edit the package.
            I.Click("#Edit_VersionTitleButton");
            string newTitle = String.Format("This title is accurate as of {0}.", DateTime.Now.ToString("F"));
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
            Assert.IsTrue(applied, "The edit doesn't appear to have been applied after ten minutes.");
        }

    }
}
