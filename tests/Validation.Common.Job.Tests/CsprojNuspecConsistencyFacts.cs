// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class CsprojNuspecConsistencyFacts
    {
        [Fact]
        public void CsprojReferencesMatchNuspec()
        {
            var sln = TryFindFile(Directory.GetCurrentDirectory(), "NuGet.Jobs.sln");
            var csproj = new FileInfo(Path.Combine(sln.Directory.FullName, @"src\Validation.Common.Job\Validation.Common.Job.csproj"));
            var nuspec = new FileInfo(Path.Combine(sln.Directory.FullName, @"src\Validation.Common.Job\Validation.Common.Job.nuspec"));

            var csprojDoc = XDocument.Load(csproj.FullName);
            var nuspecDoc = XDocument.Load(nuspec.FullName);

            var csprojPackageReferences = csprojDoc.Root.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Where(e => !IsCsprojDevDependency(e)) // Development dependencies do not need to appear in the .nuspec.
                .Select(GetCsprojDependencyInfo)
                .ToList();

            var nuspecDependencies = nuspecDoc.Root.Descendants()
                .Where(e => e.Name.LocalName == "dependency")
                .Select(GetNuspecDependencyInfo)
                .ToList();
            
            foreach (var csprojRef in csprojPackageReferences)
            {
                Assert.Contains(csprojRef, nuspecDependencies);
            }
        }

        private FileInfo TryFindFile(string startDirectory, string filename)
        {
            var path = new DirectoryInfo(startDirectory);

            FileInfo[] files = null;

            while (path != null && (files = path.GetFiles()).Count(f => f.Name == filename) <= 0)
            {
                path = path.Parent;
            }

            return files?.FirstOrDefault(f => f.Name == filename);
        }

        private DependencyInfo GetCsprojDependencyInfo(XElement xelement)
        {
            string id = xelement.Attribute("Include").Value;
            var versionAttribute = xelement.Attributes().FirstOrDefault(xa => xa.Name.LocalName == "Version");
            var versionChild = xelement.Elements().FirstOrDefault(xa => xa.Name.LocalName == "Version");
            string version = null;

            if (versionChild != null)
            {
                version = versionChild.Value;
            }
            else if (versionAttribute != null)
            {
                version = versionAttribute.Value;
            }

            Assert.NotNull(id);
            Assert.NotNull(version);

            return new DependencyInfo(id, version);
        }

        /// <remarks>
        /// Unfortunately, there is no straight-forward way to determine if a dependency is only for development.
        /// These are the set of attributes that the current tooling adds automatically to all development dependencies.
        /// In the future, these attributes or their values may change.
        /// </remarks>
        private bool IsCsprojDevDependency(XElement xelement)
        {
            var privateAssetsChild = xelement.Elements().FirstOrDefault(x => x.Name.LocalName == "PrivateAssets");
            if (privateAssetsChild == null || privateAssetsChild.Value != "all")
            {
                return false;
            }

            var includeAssetsChild = xelement.Elements().FirstOrDefault(x => x.Name.LocalName == "IncludeAssets");
            if (includeAssetsChild == null || includeAssetsChild.Value != "runtime; build; native; contentfiles; analyzers")
            {
                return false;
            }

            return true;
        }

        private DependencyInfo GetNuspecDependencyInfo(XElement xelement)
        {
            var idAttribute = xelement.Attributes().FirstOrDefault(xa => xa.Name.LocalName == "id");
            var versionAttribute = xelement.Attributes().FirstOrDefault(xa => xa.Name.LocalName == "version");

            Assert.NotNull(idAttribute);
            Assert.NotNull(versionAttribute);

            return new DependencyInfo(idAttribute.Value, versionAttribute.Value);
        }

        private class DependencyInfo : IEquatable<DependencyInfo>
        {
            public string PackageId { get; }
            public string PackageVersion { get; }

            public DependencyInfo(string packageId, string packageVersion)
            {
                PackageId = packageId;
                PackageVersion = packageVersion;
            }

            public bool Equals(DependencyInfo other)
            {
                return PackageId.Equals(other.PackageId, StringComparison.OrdinalIgnoreCase)
                    && PackageVersion.Equals(other.PackageVersion, StringComparison.OrdinalIgnoreCase);
            }

            public override string ToString()
            {
                return $"{PackageId} {PackageVersion}";
            }
        }
    }
}
