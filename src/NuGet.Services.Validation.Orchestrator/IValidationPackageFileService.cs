// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IValidationFileService
    {
        /// <summary>
        /// The action to delete the package file in the validation container.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be deleted.</param>
        /// <returns></returns>
        Task DeleteValidationPackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// The action to delete the package file.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be deleted.</param>
        /// <returns></returns>
        Task DeletePackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// It will return true if the package associated with this validation set exists.
        /// </summary>
        /// <param name="validationSet">The <see cref="PackageValidationSet"/> for the file to be found.</param>
        /// <returns></returns>
        Task<bool> DoesPackageFileExistAsync(PackageValidationSet validationSet);

        /// <summary>
        /// It will return true if the package in the validation container associated with this validation set exists.
        /// </summary>
        /// <param name="validationSet"></param>
        /// <returns></returns>
        Task<bool> DoesValidationPackageFileExistAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Download the package content from the packages container to a temporary location on disk.
        /// </summary>
        /// <param name="package">The package metadata.</param>
        /// <param name="sasDefinition">The sas definition stored on key vault.</param>
        /// <returns>The package stream.</returns>
        Task<Stream> DownloadPackageFileToDiskAsync(PackageValidationSet package, string sasDefinition = null);

        /// <summary>
        /// Backs up the package file from the location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <param name="sasDefinition">The sas definition stored on key vault.</param>
        Task BackupPackageFileFromValidationSetPackageAsync(PackageValidationSet validationSet, string sasDefinition = null);

        /// <summary>
        /// Copy a package from the validation container to a location specific for the validation set. This allows the
        /// validation set to have its own copy of the package to mutate (via <see cref="INuGetProcessor"/>) and validate.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package from the packages container to a location specific for the validation set. This allows the
        /// validation set to have its own copy of the package to mutate (via <see cref="INuGetProcessor"/>) and validate.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <returns>The etag of the source package.</returns>
        Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package from a location specific for the validation set to the packages container.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <param name="destAccessCondition">
        /// The access condition used for asserting the state of the destination file.
        /// </param>
        Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition);

        /// <summary>
        /// Copy a package from the validation container to the packages container.
        /// </summary>
        /// <param name="validationSet">The validation set.</param>
        Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Copy a package URL to a location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set.</param>
        /// <param name="srcPackageUrl">The source package URL.</param>
        /// <returns></returns>
        Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl);

        /// <summary>
        /// Delete a package from a location specific for the validation set.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Generates the URI for the specified validating package, which can be used to download it.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <param name="sasDefinition">The sas definition stored on key vault.</param>
        /// <param name="endOfAccess">The timestamp that limits the URI usage period.</param>
        /// <returns>Time limited (if implementation supports) URI for the package.</returns>
        Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, string sasDefinition, DateTimeOffset endOfAccess);

        /// <summary>
        /// Checks whether the validation set's package file exists.
        /// </summary>
        /// <param name="validationSet">The validation set, containing validation set and package identifiers.</param>
        /// <returns>True if file exists, false otherwise</returns>
        Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Updates package blob metadata in the validation set folder of the validation container.
        /// </summary>
        /// <param name="validationSet">A validationSet that will identify the package that will have its blob metadata updated.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{PackageStreamMetadata}.Result" />) returns
        /// a <see name="PackageStreamMetadata" />.</returns>
        /// <exception cref="Microsoft.WindowsAzure.Storage.StorageException">Thrown if the blob has changed between
        /// successive read and write operations.</exception>
        Task<PackageStreamMetadata> UpdatePackageBlobMetadataInValidationSetAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Updates package blob metadata in the validation container.
        /// </summary>
        /// <param name="validationSet">A validationSet that will identify the package that will have its blob metadata updated.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{PackageStreamMetadata}.Result" />) returns
        /// a <see name="PackageStreamMetadata" />.</returns>
        /// <exception cref="Microsoft.WindowsAzure.Storage.StorageException">Thrown if the blob has changed between
        /// successive read and write operations.</exception>
        Task<PackageStreamMetadata> UpdatePackageBlobMetadataInValidationAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Updates package blob properties.
        /// </summary>
        /// <param name="validationSet">A validationSet that will identify the package that will have its blob metadata updated.</param>
        /// <exception cref="Microsoft.WindowsAzure.Storage.StorageException">Thrown if the blob has changed between
        /// successive read and write operations.</exception>
        Task UpdatePackageBlobPropertiesAsync(PackageValidationSet validationSet);

        /// <summary>
        /// Reads the ETag for the package in the public container.
        /// </summary>
        /// <param name="validationSet">A validationSet that will identify the package that will have its blob metadata updated.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The result is the etag of the package blob or null if the package does not exists.</returns>
        Task<string> GetPublicPackageBlobETagOrNullAsync(PackageValidationSet validationSet);
    }
}