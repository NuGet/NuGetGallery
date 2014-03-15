using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.IO.Compression;

namespace NuGetGallery.FunctionTests.Helpers
{
    /// <summary>
    /// This class provides the helper methods to create different types of packages
    /// </summary>
    public class PackageCreationHelper
    {

        #region PublicMethods
       
        /// <summary>
        /// Creates a package given the package name and version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreatePackage(string packageName, string version = "1.0.0", string minClientVersion = null, string title = null, string tags = null, string description = null, string licenseUrl = null, string dependencies = null)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = NuspecHelper.CreateDefaultNuspecFile(packageName, version, minClientVersion, title, tags, description, licenseUrl, dependencies);
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            return CreatePackageInternal(ref standardOutput, ref standardError, nuspecFileFullPath);
        }

        /// <summary>
        /// Creates a windows 8 curated package given the package name and version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreateWindows8CuratedPackage(string packageName, string version = "1.0.0")
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = NuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddWindows8Tag(nuspecFileFullPath);
            return CreatePackageInternal(ref standardOutput, ref standardError, nuspecFileFullPath);
        }

        /// <summary>
        /// Creates a windows 8 curated package given the package name and version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreateWebMatrixCuratedPackage(string packageName, string version = "1.0.0")
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = NuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddWebMatrixTag(nuspecFileFullPath);
            return CreatePackageInternal(ref standardOutput, ref standardError, nuspecFileFullPath);
        }


        /// <summary>
        /// Creates a package with the specified minclient version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreatePackageWithMinClientVersion(string packageName, string version, string minClientVersion)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = NuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddMinClientVersionAttribute(nuspecFileFullPath, minClientVersion);
            return CreatePackageInternal(ref standardOutput, ref standardError, nuspecFileFullPath);
        }

        /// <summary>
        /// Creates a package with the specified framework version folder.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreatePackageWithTargetFramework(string packageName, string packageVersion, string frameworkVersion)
        {
            string standardOutput = string.Empty;
            string standardError = string.Empty;
            string nuspecFileFullPath = NuspecHelper.CreateDefaultNuspecFile(packageName, packageVersion);
            return CreatePackageWithTargetFrameworkInternal(ref standardOutput, ref standardError, nuspecFileFullPath, frameworkVersion);
        }

        /// <summary>
        /// Creates a package which will grow up to a huge size when extracted.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static string CreateGalleryTestBombPackage(string packageName, string version = "1.0.0")
        {
            string path = CreatePackage(packageName, version);
            WeaponizePackage(path);
            return path;
        }

        #endregion PublicMethods
        #region InternalMethods

        /// <summary>
        /// Adds default contents dir in the nuspec file location.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddContent(string nuspecFileDir)
        {
            string contentsDir = Path.Combine(nuspecFileDir, "contents");
            Directory.CreateDirectory(contentsDir);
            StreamWriter sw = new StreamWriter(Path.Combine(contentsDir, @"Samplecontent.txt"));
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Adds default contents dir with framework subfolder in the nuspec file location.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddContent(string nuspecFileDir, string frameworkVersion)
        {
            string contentsDir = Path.Combine(nuspecFileDir, "contents");
            Directory.CreateDirectory(contentsDir + "\\" + frameworkVersion);
            StreamWriter sw = new StreamWriter(Path.Combine(contentsDir, @"Samplecontent.txt"));
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Adds default Lib dir by adding a codedom generated assembly.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddLib(string nuspecFileDir)
        {
            Directory.CreateDirectory(Path.Combine(nuspecFileDir, "Lib"));
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/optimize /unsafe";
            parameters.OutputAssembly = (Path.Combine(nuspecFileDir, @"Lib", DateTime.Now.Ticks.ToString() + ".dll"));
            CSharpCodeProvider provider = new CSharpCodeProvider();
            string source = "using System; namespace CodeDom { public class B {public static int k=7;}}";
            CompilerResults r = provider.CompileAssemblyFromSource(parameters, source);
        }

        /// <summary>
        /// Adds default Lib dir and framework subfolder by adding a codedom generated assembly.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddLib(string nuspecFileDir, string frameworkVersion)
        {
            Directory.CreateDirectory(Path.Combine(nuspecFileDir, "Lib\\" + frameworkVersion));
            System.CodeDom.Compiler.CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/optimize /unsafe";
            parameters.OutputAssembly = (Path.Combine(nuspecFileDir, "Lib\\" + frameworkVersion, DateTime.Now.Ticks.ToString() + ".dll"));
            CSharpCodeProvider provider = new CSharpCodeProvider();
            string source = "using System; namespace CodeDom { public class B {public static int k=7;}}";
            CompilerResults r = provider.CompileAssemblyFromSource(parameters, source);
        }

        private static string CreatePackageInternal(ref string standardOutput, ref string standardError, string nuspecFileFullPath)
        {
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            AddContent(nuspecDir);
            AddLib(nuspecDir);
            CmdLineHelper.InvokeNugetProcess(string.Join(string.Empty, new string[] {CmdLineHelper.PackCommandString, @"""" + nuspecFileFullPath + @"""", CmdLineHelper.OutputDirectorySwitchString, @"""" + nuspecDir + @"""" }), out standardError, out standardOutput, Path.GetFullPath(Path.GetDirectoryName(nuspecFileFullPath)));
            string[] nupkgFiles = Directory.GetFiles(nuspecDir, "*.nupkg").ToArray();
            if (nupkgFiles == null || nupkgFiles.Length == 0)
                return null;
            else
                return nupkgFiles[0];
        }
        private static string CreatePackageWithTargetFrameworkInternal(ref string standardOutput, ref string standardError, string nuspecFileFullPath, string frameworkVersion)
        {
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            AddContent(nuspecDir, frameworkVersion);
            AddLib(nuspecDir, frameworkVersion);
            CmdLineHelper.InvokeNugetProcess(string.Join(string.Empty, new string[] { CmdLineHelper.PackCommandString, @"""" + nuspecFileFullPath + @"""", CmdLineHelper.OutputDirectorySwitchString, @"""" + nuspecDir + @"""" }), out standardError, out standardOutput, Path.GetFullPath(Path.GetDirectoryName(nuspecFileFullPath)));
            string[] nupkgFiles = Directory.GetFiles(nuspecDir, "*.nupkg").ToArray();
            if (nupkgFiles == null || nupkgFiles.Length == 0)
                return null;
            else
                return nupkgFiles[0];
        }

        private static void WeaponizePackage(string PackageFullPath)
        {
            var archive = new ZipArchive(new FileStream(PackageFullPath, FileMode.Open), ZipArchiveMode.Update);
            var entry = archive.GetEntry("_rels/.rels");
            Stream f = entry.Open();
            f.Position = 0;
            byte[] bytes = new byte[1100];
            for (int i = 0; i < 260144; i++)
            {
                f.Write(bytes, 0, bytes.Length);
            }
            f.Close();
            archive.Dispose();
        }

        #endregion InternalMethods
    }
}
