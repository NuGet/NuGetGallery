﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CSharp;
using Xunit.Abstractions;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// This class provides the helper methods to create different types of packages
    /// </summary>
    public class PackageCreationHelper
        : HelperBase
    {
        public PackageCreationHelper()
            : this(ConsoleTestOutputHelper.New)
        {
        }

        public PackageCreationHelper(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        /// <summary>
        /// Creates a package given the package name and version.
        /// </summary>
        public async Task<string> CreatePackage(string packageName, string version = "1.0.0", string minClientVersion = null, string title = null, string tags = null, string description = null, string licenseUrl = null, string dependencies = null)
        {
            var nuspecHelper = new NuspecHelper(TestOutputHelper);
            string nuspecFileFullPath = await nuspecHelper.CreateDefaultNuspecFile(packageName, version, minClientVersion, title, tags, description, licenseUrl, dependencies);
            var path = await CreatePackageInternal(nuspecFileFullPath);
            return path;
        }

        /// <summary>
        /// Creates a windows 8 curated package given the package name and version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task<string> CreateWindows8CuratedPackage(string packageName, string version = "1.0.0")
        {
            var nuspecHelper = new NuspecHelper(TestOutputHelper);
            string nuspecFileFullPath = await nuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddWindows8Tag(nuspecFileFullPath);
            return await CreatePackageInternal(nuspecFileFullPath);
        }

        /// <summary>
        /// Creates a windows 8 curated package given the package name and version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task<string> CreateWebMatrixCuratedPackage(string packageName, string version = "1.0.0")
        {
            var nuspecHelper = new NuspecHelper(TestOutputHelper);
            string nuspecFileFullPath = await nuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddWebMatrixTag(nuspecFileFullPath);
            return await CreatePackageInternal(nuspecFileFullPath);
        }


        /// <summary>
        /// Creates a package with the specified minclient version.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <param name="minClientVersion"></param>
        /// <returns></returns>
        public async Task<string> CreatePackageWithMinClientVersion(string packageName, string version, string minClientVersion)
        {
            var nuspecHelper = new NuspecHelper(TestOutputHelper);
            string nuspecFileFullPath = await nuspecHelper.CreateDefaultNuspecFile(packageName, version);
            NuspecHelper.AddMinClientVersionAttribute(nuspecFileFullPath, minClientVersion);
            return await CreatePackageInternal(nuspecFileFullPath);
        }

        /// <summary>
        /// Creates a package with the specified framework version folder.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="packageVersion"></param>
        /// <param name="frameworkVersion"></param>
        /// <returns></returns>
        public async Task<string> CreatePackageWithTargetFramework(string packageName, string packageVersion, string frameworkVersion)
        {
            var nuspecHelper = new NuspecHelper(TestOutputHelper);
            string nuspecFileFullPath = await nuspecHelper.CreateDefaultNuspecFile(packageName, packageVersion);
            return await CreatePackageWithTargetFrameworkInternal(nuspecFileFullPath, frameworkVersion);
        }

        /// <summary>
        /// Creates a package which will grow up to a huge size when extracted.
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task<string> CreateGalleryTestBombPackage(string packageName, string version = "1.0.0")
        {
            string path = await CreatePackage(packageName, version);
            WeaponizePackage(path);
            return path;
        }

        /// <summary>
        /// Adds default contents dir in the nuspec file location.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        internal static void AddContent(string nuspecFileDir)
        {
            string contentsDir = Path.Combine(nuspecFileDir, "contents");
            Directory.CreateDirectory(contentsDir);
            var sw = new StreamWriter(Path.Combine(contentsDir, @"Samplecontent.txt"));
            sw.Flush();
            sw.Close();
        }

        /// <summary>
        /// Adds default contents dir with framework subfolder in the nuspec file location.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        /// <param name="frameworkVersion"></param>
        internal static void AddContent(string nuspecFileDir, string frameworkVersion)
        {
            string contentsDir = Path.Combine(nuspecFileDir, "contents");
            Directory.CreateDirectory(contentsDir + "\\" + frameworkVersion);
            var sw = new StreamWriter(Path.Combine(contentsDir, @"Samplecontent.txt"));
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
            var parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/optimize /unsafe";
            parameters.OutputAssembly = (Path.Combine(nuspecFileDir, @"Lib", DateTime.Now.Ticks + ".dll"));
            var provider = new CSharpCodeProvider();
            string source = "using System; namespace CodeDom { public class B {public static int k=7;}}";
            provider.CompileAssemblyFromSource(parameters, source);
        }

        /// <summary>
        /// Adds default Lib dir and framework subfolder by adding a codedom generated assembly.
        /// </summary>
        /// <param name="nuspecFileDir"></param>
        /// <param name="frameworkVersion"></param>
        internal static void AddLib(string nuspecFileDir, string frameworkVersion)
        {
            Directory.CreateDirectory(Path.Combine(nuspecFileDir, "Lib\\" + frameworkVersion));
            var parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.CompilerOptions = "/optimize /unsafe";
            parameters.OutputAssembly = (Path.Combine(nuspecFileDir, "Lib\\" + frameworkVersion, DateTime.Now.Ticks + ".dll"));
            var provider = new CSharpCodeProvider();
            string source = "using System; namespace CodeDom { public class B {public static int k=7;}}";
            provider.CompileAssemblyFromSource(parameters, source);
        }

        private async Task<string> CreatePackageInternal(string nuspecFileFullPath)
        {
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            AddContent(nuspecDir);
            AddLib(nuspecDir);

            var commandlineHelper = new CommandlineHelper(TestOutputHelper);
            await commandlineHelper.PackPackageAsync(nuspecFileFullPath, nuspecDir);

            string[] nupkgFiles = Directory.GetFiles(nuspecDir, "*.nupkg").ToArray();
            return nupkgFiles.Length == 0 ? null : nupkgFiles[0];
        }

        private async Task<string> CreatePackageWithTargetFrameworkInternal(string nuspecFileFullPath, string frameworkVersion)
        {
            string nuspecDir = Path.GetDirectoryName(nuspecFileFullPath);
            AddContent(nuspecDir, frameworkVersion);
            AddLib(nuspecDir, frameworkVersion);

            var commandlineHelper = new CommandlineHelper(TestOutputHelper);
            await commandlineHelper.PackPackageAsync(nuspecFileFullPath, nuspecDir);

            string[] nupkgFiles = Directory.GetFiles(nuspecDir, "*.nupkg").ToArray();
            return nupkgFiles.Length == 0 ? null : nupkgFiles[0];
        }

        private static void WeaponizePackage(string packageFullPath)
        {
            var archive = new ZipArchive(new FileStream(packageFullPath, FileMode.Open), ZipArchiveMode.Update);
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
    }
}
