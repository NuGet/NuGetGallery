// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class DatabaseMigrationFacts
    {
        private XDocument _csprojDocument;

        public DatabaseMigrationFacts()
        {
            var sln = TryFindFile(Directory.GetCurrentDirectory(), "NuGetGallery.sln");
            var csproj = new FileInfo(Path.Combine(sln.Directory.FullName, @"src\NuGet.Services.DatabaseMigration\NuGet.Services.DatabaseMigration.csproj"));

            _csprojDocument = XDocument.Load(csproj.FullName);
        }

        [Fact]
        public void VerifyEntityFrameworkVersion()
        {
            var csprojPackageReferences = _csprojDocument.Root.Descendants().Where(e => e.Name.LocalName == "PackageReference").ToList();
            var entityframeworkElement = csprojPackageReferences.Where(d => d.Attribute("Include").Value.Equals("EntityFramework", StringComparison.OrdinalIgnoreCase)).Single();
            Assert.NotNull(entityframeworkElement);

            var entityframeworkVersion = entityframeworkElement.Elements().FirstOrDefault(xa => xa.Name.LocalName == "Version");
            Assert.NotNull(entityframeworkVersion);
            Assert.Equal("6.2.0", entityframeworkVersion.Value);
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
