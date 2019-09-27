// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Metadata.Catalog
{
    public static class TelemetryConstants
    {
        public const string BatchItemCount = "BatchItemCount";
        public const string BlobModified = "BlobModified";
        public const string CatalogIndexReadDurationSeconds = "CatalogIndexReadDurationSeconds";
        public const string CatalogIndexWriteDurationSeconds = "CatalogIndexWriteDurationSeconds";
        public const string IndexCommitDurationSeconds = "IndexCommitDurationSeconds";
        public const string IndexCommitTimeout = "IndexCommitTimeout";
        public const string HandlerFailedToProcessPackage = "HandlerFailedToProcessPackage";
        public const string PackageMissingHash = "PackageMissingHash";
        public const string PackageHasIncorrectHash = "PackageHasIncorrectHash";
        public const string PackageAlreadyHasHash = "PackageAlreadyHasHash";
        public const string PackageHashFixed = "PackageHashFixed";
        public const string ContentBaseAddress = "ContentBaseAddress";
        public const string GalleryBaseAddress = "GalleryBaseAddress";
        public const string ContentLength = "ContentLength";
        public const string CreatedPackagesCount = "CreatedPackagesCount";
        public const string CreatedPackagesSeconds = "CreatedPackagesSeconds";
        public const string DeletedPackagesCount = "DeletedPackagesCount";
        public const string DeletedPackagesSeconds = "DeletedPackagesSeconds";
        public const string Destination = "Destination";
        public const string EditedPackagesCount = "EditedPackagesCount";
        public const string EditedPackagesSeconds = "EditedPackagesSeconds";
        public const string HttpHeaderDurationSeconds = "HttpHeaderDurationSeconds";
        public const string Id = "Id";
        public const string JobLoopSeconds = "JobLoopSeconds";
        public const string Method = "Method";
        public const string NonExistentBlob = "NonExistentBlob";
        public const string NonExistentPackageHash = "NonExistentPackageHash";
        public const string PackageBlobReadSeconds = "PackageBlobReadSeconds";
        public const string PackageDownloadSeconds = "PackageDownloadSeconds";
        public const string ProcessBatchSeconds = "ProcessBatchSeconds";
        public const string ProcessGraphsSeconds = "ProcessGraphsSeconds";
        public const string ProcessPackageDeleteSeconds = "ProcessPackageDeleteSeconds";
        public const string ProcessPackageDetailsSeconds = "ProcessPackageDetailsSeconds";
        public const string ProcessPackageVersionIndexSeconds = "ProcessPackageVersionIndexSeconds";
        public const string RegistrationDeltaCount = "RegistrationDeltaCount";
        public const string SizeInBytes = "SizeInBytes";
        public const string StatusCode = "StatusCode";
        public const string Success = "Success";
        public const string Uri = "Uri";
        public const string UsePackageSourceFallback = "UsePackageSourceFallback";
        public const string Version = "Version";
        public const string Handler = "Handler";
        public const string ExternalIconProcessing = "ExternalIconProcessing";
        public const string EmbeddedIconProcessing = "EmbeddedIconProcessing";
        public const string IconDeletionFailed = "IconDeletionFailed";
        public const string IconDeletionSucceeded = "IconDeletionSucceeded";
        public const string ExternalIconIngestionSucceeded = "ExternalIconIngestionSucceeded";
        public const string ExternalIconIngestionFailed = "ExternalIconIngestionFailed";
        public const string IconExtractionSucceeded = "IconExtractionSucceeded";
        public const string IconExtractionFailed = "IconExtractionFailed";
        public const string GetPackageDetailsSeconds = "GetPackageDetailsSeconds";
        public const string GetPackageSeconds = "GetPackageSeconds";
        public const string CursorValue = "CursorValue";
    }
}