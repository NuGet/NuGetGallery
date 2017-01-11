﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// This class provides the helper methods to create and update the nuspec file.
    /// </summary>
    public class NuspecHelper
        : HelperBase
    {
        internal static string SampleDependency = "SampleDependency";
        internal static string SampleDependencyVersion = "1.0";

        public NuspecHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        /// <summary>
        /// Creates a Nuspec file given the Package Name.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <param name="minClientVersion"></param>
        /// <param name="title"></param>
        /// <param name="tags"></param>
        /// <param name="description"></param>
        /// <param name="licenseUrl"></param>
        /// <param name="dependencies"></param>
        /// <returns></returns>
        public async Task<string> CreateDefaultNuspecFile(string packageName, string version = "1.0.0", string minClientVersion = null, string title = null, string tags = null, string description = null, string licenseUrl = null, string dependencies = null)
        {
            string packageDir = Path.Combine(Environment.CurrentDirectory, packageName);
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }

            Directory.CreateDirectory(packageDir);
            
            var commandlineHelper = new CommandlineHelper(TestOutputHelper);
            await commandlineHelper.SpecPackageAsync(packageName, packageDir);

            string filePath = Path.Combine(packageDir, packageName + ".nuspec");
            RemoveSampleNuspecValues(filePath);
            UpdateNuspecFile(filePath, "1.0.0", version);
            UpdateNuspecFile(filePath, "Package description", "This is a test package created by the NuGet team.");
            // Apply the minClientVersion to the spec only if it's defined.
            if (minClientVersion != null)
            {
                UpdateNuspecFile(filePath, "<metadata>", String.Format("<metadata minClientVersion=\"{0}\">", minClientVersion));
            }
            if (title != null)
            {
                UpdateNuspecFile(filePath, "</metadata>", String.Format("<title>{0}</title></metadata>", title));
            }
            if (tags != null)
            {
                UpdateNuspecFile(filePath, "Tag1 Tag2", tags);
            }
            if (description != null)
            {
                UpdateNuspecFile(filePath, "This is a test package created by the NuGet team.", description);
            }
            if (licenseUrl != null)
            {
                UpdateNuspecFile(filePath, "</metadata>", String.Format("<licenseUrl>{0}</licenseUrl></metadata>", licenseUrl));
            }
            if (dependencies != null)
            {
                UpdateNuspecFile(filePath, "</dependencies>", String.Format("{0}</dependencies>", dependencies));
            }
            return filePath;
        }

        /// <summary>
        /// Given a nupsec file path, add the windows 8 tag to it.
        /// </summary>
        /// <param name="nuspecFilePath"></param>
        internal static void AddWindows8Tag(string nuspecFilePath)
        {
            UpdateNuspecFile(nuspecFilePath, "<tags>Tag1 Tag2</tags>", "<tags>Windows8</tags>");
        }

        /// <summary>
        /// Given a nupsec file path, add the Webmatrix tag to it.
        /// </summary>
        /// <param name="nuspecFilePath"></param>
        internal static void AddWebMatrixTag(string nuspecFilePath)
        {
            UpdateNuspecFile(nuspecFilePath, "<tags>Tag1 Tag2</tags>", "<tags>Asp.Net</tags>");
        }

        /// <summary>
        /// Given a nupsec file path, adds min client version tag to it.
        /// </summary>
        /// <param name="nuspecFilePath"></param>
        /// <param name="minclientVersion"></param>
        internal static void AddMinClientVersionAttribute(string nuspecFilePath, string minclientVersion)
        {
            UpdateNuspecFile(nuspecFilePath, @"<metadata>", @"<metadata minClientVersion=""" + minclientVersion + @"""" + @">");
        }

        /// <summary>
        /// Given a nuspec file, this removes the default sample nuspec values.
        /// </summary>
        /// <param name="nuspecFilePath"></param>
        internal static void RemoveSampleNuspecValues(string nuspecFilePath)
        {
            UpdateNuspecFile(nuspecFilePath, "<licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>", null);
            UpdateNuspecFile(nuspecFilePath, "<projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>", string.Empty);
            UpdateNuspecFile(nuspecFilePath, "<projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>", string.Empty);
            UpdateNuspecFile(nuspecFilePath, "<iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>", string.Empty);
            var searchString = new StringBuilder(@"<dependency id=" + @"""" + SampleDependency + @"""" + " version=" + @"""" + SampleDependencyVersion + @"""" + @" />");
            UpdateNuspecFile(nuspecFilePath, searchString.ToString(), string.Empty);
        }

        /// <summary>
        /// Given a nuspec file, this method will replace the search string with the replacement string.
        /// </summary>
        /// <param name="nuspecFilepath"></param>
        /// <param name="searchString"></param>
        /// <param name="replacementString"></param>
        internal static void UpdateNuspecFile(string nuspecFilepath, string searchString, string replacementString)
        {
            //Update contents Nuspec file.
            if (File.Exists(nuspecFilepath))
            {
                string specText = File.ReadAllText(nuspecFilepath);
                specText = specText.Replace(searchString, replacementString);
                File.WriteAllLines(nuspecFilepath, new[] { specText });
            }
        }
    }
}
