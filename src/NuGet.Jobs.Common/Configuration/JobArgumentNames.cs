// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs
{
    /// <summary>
    /// Keep the argument names as lower case for simple string match
    /// </summary>
    public static class JobArgumentNames
    {
        // Job argument names
        public const string Once = "Once";
        public const string Sleep = "Sleep";

        // Database argument names
        public const string SourceDatabase = "SourceDatabase";
        public const string DestinationDatabase = "DestinationDatabase";
        public const string PackageDatabase = "PackageDatabase";

        // Storage Argument names
        public const string TargetStorageAccount = "TargetStorageAccount";
        public const string TargetStoragePath = "TargetStoragePath";

        // Catalog argument names
        public const string CatalogStorage = "CatalogStorage";
        public const string CatalogPath = "CatalogPath";
        public const string CatalogPageSize = "CatalogPageSize";
        public const string CatalogIndexUrl = "CatalogIndexUrl";
        public const string CatalogIndexPath = "CatalogIndexPath";
        public const string DontStoreCursor = "DontStoreCursor";

        // Catalog Collector argument names
        public const string ChecksumCollectorBatchSize = "ChecksumCollectorBatchSize";

        // Target Argument names
        public const string TargetBaseAddress = "TargetBaseAddress";
        public const string TargetLocalDirectory = "TargetLocalDirectory";

        // Other Argument names
        public const string CdnBaseAddress = "CdnBaseAddress";
        public const string GalleryBaseAddress = "GalleryBaseAddress";

        // Arguments specific to ArchivePackages job
        public const string Source = "Source";
        public const string PrimaryDestination = "PrimaryDestination";
        public const string SecondaryDestination = "SecondaryDestination";
        public const string SourceContainerName = "SourceContainerName";
        public const string DestinationContainerName = "DestinationContainerName";
        public const string CursorBlob = "CursorBlob";

        //Arguments specific to CreateWarehouseReports job
        public const string WarehouseStorageAccount = "WarehouseStorageAccount";
        public const string WarehouseContainerName = "WarehouseContainerName";

        // Arguments specific to Search* jobs
        public const string DataStorageAccount = "DataStorageAccount";
        public const string DataContainerName = "DataContainerName";
        public const string LocalIndexFolder = "LocalIndexFolder";
        public const string IndexFolder = "IndexFolder";
        public const string ContainerName = "ContainerName";

        //Other
        public const string CommandTimeOut = "CommandTimeOut";
        public const string RankingCount = "RankingCount";
        public const string RetryCount = "RetryCount";
        public const string MaxManifestSize = "MaxManifestSize";
        public const string OutputDirectory = "OutputDirectory";

        //Arguments specific to HandlePackageEdits
        public const string SourceStorage = "SourceStorage";
        public const string BackupStorage = "BackupStorage";
        public const string BackupContainerName = "BackupContainerName";

        //Arguments specific to UpdateLicenseReports
        public const string LicenseReportService = "LicenseReportService";
        public const string LicenseReportUser = "LicenseReportUser";
        public const string LicenseReportPassword = "LicenseReportPassword";

        //Arguments specific to CollectAzureCdnLogs
        public const string FtpSourceUri = "FtpSourceUri";
        public const string FtpSourceUsername = "FtpSourceUsername";
        public const string FtpSourcePassword = "FtpSourcePassword";
        public const string AzureCdnAccountNumber = "AzureCdnAccountNumber";
        public const string AzureCdnPlatform = "AzureCdnPlatform";

        //Arguments shared by CollectAzureCdnLogs and ParseAzureCdnLogs
        public const string AzureCdnCloudStorageAccount = "AzureCdnCloudStorageAccount";
        public const string AzureCdnCloudStorageContainerName = "AzureCdnCloudStorageContainerName";

        //Arguments specific to ParseAzureCdnLogs
        public const string AzureCdnCloudStorageTableName = "AzureCdnCloudStorageTableName";

        //Arguments specific to Heartbeat
        public const string HeartbeatConfig = "HeartbeatConfig";
        public const string DashboardStorageAccount = "DashboardStorageAccount";
        public const string DashboardStorageContainer = "DashboardStorageContainer";
        public const string LogFileSuffix = "LogFileSuffix";
    }
}