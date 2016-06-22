// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class ReflowPackageService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;

        public ReflowPackageService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService)
        {
            _entitiesContext = entitiesContext;
            _packageService = packageService;
            _packageFileService = packageFileService;
        }

        public async Task<Package> ReflowAsync(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersion(id, version);

            if (package == null)
            {
                return null;
            }

            EntitiesConfiguration.SuspendExecutionStrategy = true;
            using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
            {
                // 1) Download package binary to memory
                using (var packageStream = await _packageFileService.DownloadPackageFileAsync(package))
                {
                    using (var packageArchive = new PackageArchiveReader(packageStream, leaveStreamOpen: false))
                    {
                        // 2) Determine package metadata from binary
                        var packageStreamMetadata = new PackageStreamMetadata
                        {
                            HashAlgorithm = Constants.Sha512HashAlgorithmId,
                            Hash = CryptographyService.GenerateHash(packageStream.AsSeekableStream()),
                            Size = packageStream.Length,
                        };

                        var packageMetadata = PackageMetadata.FromNuspecReader(packageArchive.GetNuspecReader());

                        // 3) Clear referenced objects that will be reflowed
                        ClearSupportedFrameworks(package);
                        ClearAuthors(package);
                        ClearDependencies(package);

                        // 4) Reflow the package
                        var listed = package.Listed;

                        package = _packageService.EnrichPackageFromNuGetPackage(
                            package,
                            packageArchive,
                            packageMetadata,
                            packageStreamMetadata,
                            package.User);

                        package.LastEdited = DateTime.UtcNow;
                        package.Listed = listed;

                        // 5) Save and profit
                        await _entitiesContext.SaveChangesAsync();
                    }
                }

                // Commit transaction
                transaction.Commit();
            }
            EntitiesConfiguration.SuspendExecutionStrategy = false;

            return package;
        }
        
        private void ClearSupportedFrameworks(Package package)
        {
            foreach (var supportedFramework in package.SupportedFrameworks.ToList())
            {
                _entitiesContext.Set<PackageFramework>().Remove(supportedFramework);
            }
            package.SupportedFrameworks.Clear();
        }

        private void ClearAuthors(Package package)
        {
#pragma warning disable 618
            foreach (var packageAuthor in package.Authors.ToList())
            {
                _entitiesContext.Set<PackageAuthor>().Remove(packageAuthor);
            }
            package.Authors.Clear();
#pragma warning restore 618
        }

        private void ClearDependencies(Package package)
        {
            foreach (var packageDependency in package.Dependencies.ToList())
            {
                _entitiesContext.Set<PackageDependency>().Remove(packageDependency);
            }
            package.Dependencies.Clear();
        }
    }
}