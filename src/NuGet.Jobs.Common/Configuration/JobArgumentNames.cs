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
        public const string Interval = "Interval";

        public const string WhatIf = "WhatIf";

        // Database argument names
        public const string SourceDatabase = "SourceDatabase";
        public const string DestinationDatabase = "DestinationDatabase";
        public const string PackageDatabase = "PackageDatabase";
        public const string GalleryDatabase = "GalleryDatabase";
        public const string StatisticsDatabase = "StatisticsDatabase";

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
        public const string WarehouseReportName = "WarehouseReportName";

        // Arguments specific to Search* jobs
        public const string DataStorageAccount = "DataStorageAccount";
        public const string DataContainerName = "DataContainerName";
        public const string LocalIndexFolder = "LocalIndexFolder";
        public const string IndexFolder = "IndexFolder";
        public const string ContainerName = "ContainerName";

        //Other
        public const string CommandTimeOut = "CommandTimeOut";
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
        public const string AggregatesOnly = "AggregatesOnly";

        // Arguments specific to RollUpDownloadFacts
        public const string MinAgeInDays = "MinAgeInDays";

        //Arguments specific to Heartbeat
        public const string HeartbeatConfig = "HeartbeatConfig";
        public const string DashboardStorageAccount = "DashboardStorageAccount";
        public const string DashboardStorageContainer = "DashboardStorageContainer";
        public const string LogFileSuffix = "LogFileSuffix";

        // Application Insights
        public const string InstrumentationKey = "InstrumentationKey";

        // Arguments specific to validation tasks
        public const string RunValidationTasks = "RunValidationTasks";
        public const string RequestValidationTasks = "RequestValidationTasks";

        // Arguments specific to VCS validation task
        public const string VcsValidatorServiceUrl = "VcsValidatorServiceUrl";
        public const string VcsValidatorCallbackUrl = "VcsValidatorCallbackUrl";
        public const string VcsValidatorAlias = "VcsValidatorAlias";
        public const string VcsPackageUrlTemplate = "VcsPackageUrlTemplate";

        // Key Vault
        public const string VaultName = "VaultName";
        public const string ClientId = "ClientId";
        public const string CertificateThumbprint = "CertificateThumbprint";
        public const string ValidateCertificate = "ValidateCertificate";
        public const string StoreName = "StoreName";
        public const string StoreLocation = "StoreLocation";
        public const string RefreshIntervalSec = "RefreshIntervalSec";

        // Arguments specific to e-mail
        public const string MailFrom = "MailFrom";
        public const string SmtpUri = "SmtpUri";

        // Arguments for Azure logs
        public const string LogsAzureStorageConnectionString = "LogsAzureStorageConnectionString";
    }
}