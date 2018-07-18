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
        public const string ContentBaseAddress = "ContentBaseAddress";
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
        public const string PackageDownloadSeconds = "PackageDownloadSeconds";
        public const string ProcessBatchSeconds = "ProcessBatchSeconds";
        public const string ProcessGraphsSeconds = "ProcessGraphsSeconds";
        public const string ProcessPackageDeleteSeconds = "ProcessPackageDeleteSeconds";
        public const string ProcessPackageDetailsSeconds = "ProcessPackageDetailsSeconds";
        public const string ProcessPackageVersionIndexSeconds = "ProcessPackageVersionIndexSeconds";
        public const string RegistrationDeltaCount = "RegistrationDeltaCount";
        public const string StatusCode = "StatusCode";
        public const string Success = "Success";
        public const string Uri = "Uri";
        public const string Version = "Version";
    }
}