﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Diagnostics;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageUploadService : IPackageUploadService
    {

        private readonly IPackageService _packageService;
        private readonly IPackageFileService _packageFileService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IReservedNamespaceService _reservedNamespaceService;
        private readonly IValidationService _validationService;
        private readonly ICoreLicenseFileService _coreLicenseFileService;
        private readonly ICoreReadmeFileService _coreReadmeFileService;
        private readonly IPackageVulnerabilitiesManagementService _vulnerabilityService;
        private readonly IPackageMetadataValidationService _metadataValidationService;

        public PackageUploadService(
            IPackageService packageService,
            IPackageFileService packageFileService,
            IEntitiesContext entitiesContext,
            IReservedNamespaceService reservedNamespaceService,
            IValidationService validationService,
            ICoreLicenseFileService coreLicenseFileService,
            ICoreReadmeFileService coreReadmeFileService,
            IDiagnosticsService diagnosticsService,
            IPackageVulnerabilitiesManagementService vulnerabilityService,
            IPackageMetadataValidationService metadataValidationService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _reservedNamespaceService = reservedNamespaceService ?? throw new ArgumentNullException(nameof(reservedNamespaceService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _coreLicenseFileService = coreLicenseFileService ?? throw new ArgumentNullException(nameof(coreLicenseFileService));
            _coreReadmeFileService = coreReadmeFileService ?? throw new ArgumentNullException(nameof(coreReadmeFileService));

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }
            _vulnerabilityService = vulnerabilityService ?? throw new ArgumentNullException(nameof(vulnerabilityService));
            _metadataValidationService = metadataValidationService ?? throw new ArgumentNullException(nameof(metadataValidationService));
        }

        public async Task<PackageValidationResult> ValidateBeforeGeneratePackageAsync(
            PackageArchiveReader nuGetPackage,
            PackageMetadata packageMetadata,
            User currentUser)
        {
            var result = await _metadataValidationService.ValidateMetadataBeforeUploadAsync(nuGetPackage, packageMetadata, currentUser);

            return result;
        }

        public async Task<PackageValidationResult> ValidateAfterGeneratePackageAsync(
            Package package,
            PackageArchiveReader nuGetPackage,
            User owner,
            User currentUser,
            bool isNewPackageRegistration)
        {
            var result = await _metadataValidationService.ValidateMetadaAfterGeneratePackageAsync(package, nuGetPackage, owner, currentUser, isNewPackageRegistration);

            return result;
        }

        public async Task<Package> GeneratePackageAsync(
            string id,
            PackageArchiveReader nugetPackage,
            PackageStreamMetadata packageStreamMetadata,
            User owner,
            User currentUser)
        {
            var shouldMarkIdVerified = _reservedNamespaceService.ShouldMarkNewPackageIdVerified(owner, id, out var ownedMatchingReservedNamespaces);

            var package = await _packageService.CreatePackageAsync(
                nugetPackage,
                packageStreamMetadata,
                owner,
                currentUser,
                isVerified: shouldMarkIdVerified);

            if (shouldMarkIdVerified)
            {
                // Add all relevant package registrations to the applicable namespaces
                foreach (var rn in ownedMatchingReservedNamespaces)
                {
                    _reservedNamespaceService.AddPackageRegistrationToNamespace(
                        rn.Value,
                        package.PackageRegistration);
                }
            }

            _vulnerabilityService.ApplyExistingVulnerabilitiesToPackage(package);

            return package;
        }

        public async Task<PackageCommitResult> CommitPackageAsync(Package package, Stream packageFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            if (!packageFile.CanSeek)
            {
                throw new ArgumentException($"{nameof(packageFile)} argument must be seekable stream", nameof(packageFile));
            }

            await _validationService.UpdatePackageAsync(package);

            if (package.PackageStatusKey != PackageStatus.Available
                && package.PackageStatusKey != PackageStatus.Validating)
            {
                throw new ArgumentException(
                    $"The package to commit must have either the {PackageStatus.Available} or {PackageStatus.Validating} package status.",
                    nameof(package));
            }

            try
            {
                if (package.PackageStatusKey == PackageStatus.Validating)
                {
                    await _packageFileService.SaveValidationPackageFileAsync(package, packageFile);

                    /* Suppose two package upload requests come in at the same time with the same package (same ID and
                     * version). It's possible that one request has committed and validated the package AFTER the other
                     * request has checked that this package does not exist in the database. Observe the following
                     * sequence of events to understand why the packages container check is necessary.
                     * 
                     * Request | Step                                           | Component        | Success | Notes
                     * ------- | ---------------------------------------------- | ---------------- | ------- | -----
                     * 1       | version should not exist in DB                 | gallery          | TRUE    | 1st duplicate check (catches most cases over time)
                     * 2       | version should not exist in DB                 | gallery          | TRUE    |
                     * 1       | upload to validation container                 | gallery          | TRUE    | 2nd duplicate check (relevant with high concurrency)
                     * 1       | version should not exist in packages container | gallery          | TRUE    | 3rd duplicate check (relevant with fast validations)
                     * 1       | commit to DB                                   | gallery          | TRUE    |
                     * 1       | upload to packages container                   | async validation | TRUE    |
                     * 1       | move package to Available status in DB         | async validation | TRUE    |
                     * 1       | delete from validation container               | async validation | TRUE    |
                     * 2       | upload to validation container                 | gallery          | TRUE    |
                     * 2       | version should not exist in packages container | gallery          | FALSE   |
                     * 2       | delete from validation (rollback)              | gallery          | TRUE    | Only occurs in the failure case, as a clean-up.
                     *
                     * Alternatively, we could handle the DB conflict exception that would occur in request 2, but this
                     * would result in an exception that can be avoided and require some ugly code that teases the
                     * unique constraint failure out of a SqlException.
                     * 
                     * Another alternative is always leaving the package in the validation container. This is not great
                     * since it doubles the amount of space we need to store packages. Also, it complicates the soft or
                     * hard package delete flow.
                     * 
                     * We can safely delete the validation package because we know it's ours. We know this because
                     * saving the validation package succeeded, meaning async validation already successfully moved the
                     * previous package (request 1's package) from the validation container to the package container
                     * and transitioned the package to Available status.
                     * 
                     * See the following issue in GitHub for how this case was found:
                     * https://github.com/NuGet/NuGetGallery/issues/5039
                     */
                    if (await _packageFileService.DoesPackageFileExistAsync(package))
                    {
                        await _packageFileService.DeleteValidationPackageFileAsync(
                            package.PackageRegistration.Id,
                            package.Version);

                        return PackageCommitResult.Conflict;
                    }
                }
                else
                {
                    if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        // if the package is immediately made available, it means there is a high chance we don't have
                        // validation pipeline that would normally store the license file, so we'll do it ourselves here.
                        await _coreLicenseFileService.ExtractAndSaveLicenseFileAsync(package, packageFile);
                    }

                    var isReadmeFileExtractedAndSaved = false;
                    if (package.HasReadMe && package.EmbeddedReadmeType != EmbeddedReadmeFileType.Absent)
                    {
                        await _coreReadmeFileService.ExtractAndSaveReadmeFileAsync(package, packageFile);
                        isReadmeFileExtractedAndSaved = true;
                    }

                    try
                    {
                        packageFile.Seek(0, SeekOrigin.Begin);
                        await _packageFileService.SavePackageFileAsync(package, packageFile);
                    }
                    catch when (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent || isReadmeFileExtractedAndSaved)
                    {
                        if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                        {
                            await _coreLicenseFileService.DeleteLicenseFileAsync(
                                package.PackageRegistration.Id,
                                package.NormalizedVersion);
                        }

                        if (isReadmeFileExtractedAndSaved)
                        {
                            await _coreReadmeFileService.DeleteReadmeFileAsync(
                                package.PackageRegistration.Id,
                                package.NormalizedVersion);
                        }
                        throw;
                    }
                }
            }
            catch (FileAlreadyExistsException ex)
            {
                ex.Log();
                return PackageCommitResult.Conflict;
            }

            try
            {
                // Sending the validation request after copying to prevent multiple validation requests
                // sent when several pushes for the same package happen concurrently. Copying the file
                // resolves the race and only one request will "win" and reach this code.
                await _validationService.StartValidationAsync(package);

                // commit all changes to database as an atomic transaction
                await _entitiesContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // If sending the validation request or saving to the DB fails for any reason
                // we need to delete the package we just saved.
                if (package.PackageStatusKey == PackageStatus.Validating)
                {
                    await _packageFileService.DeleteValidationPackageFileAsync(
                        package.PackageRegistration.Id,
                        package.Version);
                }
                else
                {
                    await _packageFileService.DeletePackageFileAsync(
                        package.PackageRegistration.Id,
                        package.Version);
                    await _coreLicenseFileService.DeleteLicenseFileAsync(
                        package.PackageRegistration.Id,
                        package.NormalizedVersion);
                    await _coreReadmeFileService.DeleteReadmeFileAsync(
                        package.PackageRegistration.Id,
                        package.NormalizedVersion);
                }

                if (IsConflict(ex))
                {
                    return PackageCommitResult.Conflict;
                }
                else
                {
                    throw;
                }
            }

            return PackageCommitResult.Success;
        }

        private bool IsConflict(Exception ex)
        {
            if (ex is DbUpdateConcurrencyException concurrencyEx)
            {
                return true;
            }
            else if (ex is DbUpdateException dbUpdateEx)
            {
                if (dbUpdateEx.InnerException?.InnerException != null)
                {
                    if (dbUpdateEx.InnerException.InnerException is SqlException sqlException)
                    {
                        switch (sqlException.Number)
                        {
                            case 547:   // Constraint check violation
                            case 2601:  // Duplicated key row error
                            case 2627:  // Unique constraint error
                                return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
