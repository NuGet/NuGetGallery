// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class ReflowPackageService
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly ITelemetryService _telemetryService;

        public ReflowPackageService(
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            IPackageFileService packageFileService,
            ITelemetryService telemetryService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public async Task<Package> ReflowAsync(string id, string version)
        {
            var package = _packageService.FindPackageByIdAndVersionStrict(id, version);

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
                            HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                            Hash = CryptographyService.GenerateHash(
                                packageStream.AsSeekableStream(),
                                CoreConstants.Sha512HashAlgorithmId),
                            Size = packageStream.Length,
                        };

                        var packageMetadata = PackageMetadata.FromNuspecReader(
                            packageArchive.GetNuspecReader(),
                            strict: false);

                        // 3) Clear referenced objects that will be reflowed
                        ClearSupportedFrameworks(package);
                        ClearAuthors(package);
                        ClearDependencies(package);
                        ClearPackageTypes(package);

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

                        // 5) Update IsLatest so that reflow can correct concurrent updates (see Gallery #2514)
                        await _packageService.UpdateIsLatestAsync(package.PackageRegistration, commitChanges: false);

                        // 6) Emit telemetry.
                        _telemetryService.TrackPackageReflow(package);

                        // 7) Save and profit
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

        private void ClearPackageTypes(Package package)
        {
            foreach (var packageType in package.PackageTypes.ToList())
            {
                _entitiesContext.Set<PackageType>().Remove(packageType);
            }
            package.PackageTypes.Clear();
        }
    }
}