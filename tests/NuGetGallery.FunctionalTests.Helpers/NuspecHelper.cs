using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// This class provides the helper methods to create and update the nuspec file.
    /// </summary>
    public class NuspecHelper
    {
        /// <summary>
        /// Creates a Nuspec file given the Package Name.
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public static string CreateDefaultNuspecFile(string packageName, string version = "1.0.0", string minClientVersion = null, string title = null, string tags = null, string description = null, string licenseUrl = null, string dependencies = null)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string packageDir = Path.Combine(Environment.CurrentDirectory, packageName);
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, true);
            Directory.CreateDirectory(packageDir);
            CmdLineHelper.InvokeNugetProcess(string.Join(string.Empty, new string[] { CmdLineHelper.SpecCommandString, packageName }), out standardError, out standardOutput, packageDir);
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


        #region PrivateMethods
        /// <summary>
        /// Given a nupsec file path, add the windows 8 tag to it.
        /// </summary>
        /// <param name="NuspecFilePath"></param>
        internal static void AddWindows8Tag(string NuspecFilePath)
        {
            UpdateNuspecFile(NuspecFilePath, "<tags>Tag1 Tag2</tags>", "<tags>Windows8</tags>");
        }

        /// <summary>
        /// Given a nupsec file path, add the Webmatrix tag to it.
        /// </summary>
        /// <param name="NuspecFilePath"></param>
        internal static void AddWebMatrixTag(string NuspecFilePath)
        {
            UpdateNuspecFile(NuspecFilePath, "<tags>Tag1 Tag2</tags>", "<tags>Asp.Net</tags>");
        }

        /// <summary>
        /// Given a nupsec file path, adds min client version tag to it.
        /// </summary>
        /// <param name="NuspecFilePath"></param>
        internal static void AddMinClientVersionAttribute(string NuspecFilePath, string minclientVersion)
        {
            UpdateNuspecFile(NuspecFilePath, @"<metadata>", @"<metadata minClientVersion=""" + minclientVersion + @"""" + @">");
        }       

        /// <summary>
        /// Given a nuspec file, this removes the default sample nuspec values.
        /// </summary>
        /// <param name="NuspecFilePath"></param>
        internal static void RemoveSampleNuspecValues(string NuspecFilePath)
        {
            UpdateNuspecFile(NuspecFilePath, "<licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>", null);
            UpdateNuspecFile(NuspecFilePath, "<projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>", string.Empty);
            UpdateNuspecFile(NuspecFilePath, "<projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>", string.Empty);
            UpdateNuspecFile(NuspecFilePath, "<iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>", string.Empty);
            StringBuilder searchString = new StringBuilder(@"<dependency id=" + @"""" + SampleDependency + @"""" + " version=" + @"""" + SampleDependencyVersion + @"""" + @" />");
            UpdateNuspecFile(NuspecFilePath, searchString.ToString(), string.Empty);
        }

        /// <summary>
        /// Given a nuspec file, this method will replace the search string with the replacement string.
        /// </summary>
        /// <param name="NuspecFilepath"></param>
        /// <param name="searchString"></param>
        /// <param name="replacementString"></param>
        internal static void UpdateNuspecFile(string NuspecFilepath, string searchString, string replacementString)
        {
            //Update contents Nuspec file.
            if (File.Exists(NuspecFilepath))
            {
                string specText = File.ReadAllText(NuspecFilepath);
                specText = specText.Replace(searchString, replacementString);
                File.WriteAllLines(NuspecFilepath, new string[] { specText });
            }
        }

        #endregion PrivateMethods

        #region PrivateMemebers
        internal static string SampleDependency = "SampleDependency";
        internal static string SampleDependencyVersion = "1.0";
        #endregion PrivateMemebers
    }
}
