using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGetGallery.FunctionTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAutomation;

namespace NuGetGallery.FunctionalTests.Fluent
{

    [TestClass]
    public class TagSearchTest : NuGetFluentTest
    {
        [TestMethod]
        [Description("Verify parsing of functional tags from package metadata.")]
        public void TagSearch()
        {
            string packageName = "NuGetGallery.FunctionalTests.Fluent.TagSearchTest";
            string version = "1.0.0";
            string tagString = ";This,is a,;test,,package, created ;by ,the  NuGet;;;team.";

            UploadPackageIfNecessary(packageName, version, null, null, tagString, "This is a test package created by the NuGet team.");

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
