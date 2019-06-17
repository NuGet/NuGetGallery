// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class PackageDependencyFacts
    {
        private FileInfo _sln;

        public PackageDependencyFacts()
        {
            _sln = TryFindFile(Directory.GetCurrentDirectory(), "NuGetGallery.sln");
        }

        /// <summary>
        /// 1. Verify that the version of EntityFramework equals to 6.2.0;
        /// We hit a bug of EntityFramework 6.2.0, and We use some special ways to fix it which only targets version 6.2.0.
        /// <see cref="NuGet.Services.DatabaseMigration.Job"/>: "NuGet.Services.DatabaseMigration.Job/OverwriteSqlConnection"
        /// 2. Verify that the version of Microsoft.Extensions.* package equals to 1.1.2;
        /// 3. Verify that the version of Autofac package equals to 4.2.0.
        /// </summary>
        [Theory]
        [MemberData(nameof(NuGet_Services_DatabaseMigration_PackageNameAndVersion))]
        public void VerifyPackageVersionForNuGet_Services_DatabaseMigration(string packageName, string packageVersion)
        {
            var csproj = new FileInfo(Path.Combine(_sln.Directory.FullName, @"src\NuGet.Services.DatabaseMigration\NuGet.Services.DatabaseMigration.csproj"));
            var csprojPackageReferences = XDocument.Load(csproj.FullName).Root.Descendants().Where(e => e.Name.LocalName == "PackageReference").ToList();
            var packageElement = csprojPackageReferences.Where(d => d.Attribute("Include").Value.Equals(packageName, StringComparison.OrdinalIgnoreCase)).Single();
            Assert.NotNull(packageElement);

            var packageElementVersion = packageElement.Elements().FirstOrDefault(xa => xa.Name.LocalName == "Version");
            Assert.NotNull(packageElementVersion);
            Assert.Equal(packageVersion, packageElementVersion.Value);
        }

        public static IEnumerable<object[]> NuGet_Services_DatabaseMigration_PackageNameAndVersion
        {
            get
            {
                yield return new object[] { "Autofac.Extensions.DependencyInjection", "4.2.0" };
                yield return new object[] { "EntityFramework", "6.2.0" };
                yield return new object[] { "Microsoft.Extensions.Configuration", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Configuration.Abstractions", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Configuration.Binder", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Configuration.EnvironmentVariables", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Configuration.FileExtensions", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Configuration.Json", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Logging", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Options", "1.1.2" };
            }
        }

        /// <summary>
        /// 1. Verify that the version of EntityFramework equals to 6.2.0;
        /// We hit a bug of EntityFramework 6.2.0, and We use some special ways to fix it which only targets version 6.2.0.
        /// <see cref="NuGet.Services.DatabaseMigration.Job"/>: "NuGet.Services.DatabaseMigration.Job/OverwriteSqlConnection"
        /// 2. Verify that the version of Microsoft.Extensions.* package equals to 1.1.2;
        /// 3. Verify that the version of Autofac package equals to 4.2.0.
        /// </summary>
        [Theory]
        [MemberData(nameof(DatabaseMigrationTools_PackageNameAndVersion))]
        public void VerifyPackageVersionForDatabaseMigrationTools(string packageName, string packageVersion)
        {
            var csproj = new FileInfo(Path.Combine(_sln.Directory.FullName, @"src\DatabaseMigrationTools\DatabaseMigrationTools.csproj"));
            var csprojPackageReferences = XDocument.Load(csproj.FullName).Root.Descendants().Where(e => e.Name.LocalName == "PackageReference").ToList();
            var packageElement = csprojPackageReferences.Where(d => d.Attribute("Include").Value.Equals(packageName, StringComparison.OrdinalIgnoreCase)).Single();
            Assert.NotNull(packageElement);

            var packageElementVersion = packageElement.Elements().FirstOrDefault(xa => xa.Name.LocalName == "Version");
            Assert.NotNull(packageElementVersion);
            Assert.Equal(packageVersion, packageElementVersion.Value);
        }

        public static IEnumerable<object[]> DatabaseMigrationTools_PackageNameAndVersion
        {
            get
            {
                yield return new object[] { "Autofac.Extensions.DependencyInjection", "4.2.0" };
                yield return new object[] { "EntityFramework", "6.2.0" };
                yield return new object[] { "Microsoft.Extensions.Logging", "1.1.2" };
                yield return new object[] { "Microsoft.Extensions.Options", "1.1.2" };
            }
        }

        [Theory]
        [MemberData(nameof(PackageDLLNameAndVersion))]
        public void verifyPackageDLLVersion(string packageDLLPath, string packageVersion)
        {
            Assert.Equal(packageVersion, FileVersionInfo.GetVersionInfo(packageDLLPath).ProductVersion);
        }

        public static IEnumerable<object[]> PackageDLLNameAndVersion
        {
            get
            {
                yield return new object[] { @".\Autofac.Extensions.DependencyInjection.dll", "4.2.0" };
                yield return new object[] { @".\EntityFramework.dll", "6.2.0-61023" };
                yield return new object[] { @".\Microsoft.Extensions.Logging.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Options.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.Abstractions.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.Binder.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.EnvironmentVariables.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.FileExtensions.dll", "1.1.2" };
                yield return new object[] { @".\Microsoft.Extensions.Configuration.Json.dll", "1.1.2" };
            }
        }

        private static FileInfo TryFindFile(string startDirectory, string filename)
        {
            var path = new DirectoryInfo(startDirectory);

            FileInfo[] files = null;

            while (path != null && (files = path.GetFiles()).Count(f => f.Name == filename) <= 0)
            {
                path = path.Parent;
            }

            return files?.FirstOrDefault(f => f.Name == filename);
        }
    }
}
