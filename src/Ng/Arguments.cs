// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog.Monitoring;

namespace Ng
{
    public static class Arguments
    {
        #region Shared
        public const char Prefix = '-';
        public const char Quote = '"';

        public const string DirectoryType = "directoryType";
        public const string Gallery = "gallery";
        public const string Id = "id";
        public const string InstrumentationKey = "instrumentationkey";
        public const string Path = "path";
        public const string Source = "source";
        public const string Verbose = "verbose";
        public const string InstanceName = "instanceName";

        public const int DefaultInterval = 3; // seconds
        public const string Interval = "interval";

        public const int DefaultReinitializeIntervalSec = 60 * 60; // 1 hour
        public const string ReinitializeIntervalSec = "ReinitializeIntervalSec";

        public const string LuceneDirectoryType = "luceneDirectoryType";
        public const string LucenePath = "lucenePath";
        public const string LuceneStorageAccountName = "luceneStorageAccountName";
        public const string LuceneStorageContainer = "luceneStorageContainer";
        public const string LuceneStorageKeyValue = "luceneStorageKeyValue";


        public const string AzureStorageType = "azure";
        public const string FileStorageType = "file";

        public const string ConnectionString = "connectionString";
        public const string ContentBaseAddress = "contentBaseAddress";
        public const string GalleryBaseAddress = "galleryBaseAddress";
        public const string StorageAccountName = "storageAccountName";
        public const string StorageBaseAddress = "storageBaseAddress";
        public const string StorageContainer = "storageContainer";
        public const string StorageKeyValue = "storageKeyValue";
        public const string StoragePath = "storagePath";
        public const string StorageQueueName = "storageQueueName";
        public const string StorageType = "storageType";
        public const string Version = "version";

        public const string StorageSuffix = "storageSuffix";
        public const string StorageOperationMaxExecutionTimeInSeconds = "storageOperationMaxExecutionTimeInSeconds";
        public const string StorageServerTimeoutInSeconds = "storageServerTimeoutInSeconds";
        public const string HttpClientTimeoutInSeconds = "httpClientTimeoutInSeconds";

        public const string StorageAccountNamePreferredPackageSourceStorage = "storageAccountNamePreferredPackageSourceStorage";
        public const string StorageKeyValuePreferredPackageSourceStorage = "storageKeyValuePreferredPackageSourceStorage";
        public const string StorageContainerPreferredPackageSourceStorage = "storageContainerPreferredPackageSourceStorage";

        public const string PreferAlternatePackageSourceStorage = "preferAlternatePackageSourceStorage";

        public const string StorageUseServerSideCopy = "storageUseServerSideCopy";
        public const string MaxConcurrentBatches = "maxConcurrentBatches";

        #endregion

        #region Catalog2Lucene
        public const string CatalogBaseAddress = "catalogBaseAddress";
        public const string Registration = "registration";
        public const string CommitTimeoutInSeconds = "commitTimeoutInSeconds";
        public const string FlatContainerBaseAddress = "flatContainerBaseAddress";
        #endregion

        #region Catalog2Registration
        public const string CompressedStorageAccountName = "compressedStorageAccountName";
        public const string CompressedStorageBaseAddress = "compressedStorageBaseAddress";
        public const string CompressedStorageContainer = "compressedStorageContainer";
        public const string CompressedStorageKeyValue = "compressedStorageKeyValue";
        public const string CompressedStoragePath = "compressedStoragePath";

        public const string SemVer2StorageAccountName = "semVer2StorageAccountName";
        public const string SemVer2StorageBaseAddress = "semVer2StorageBaseAddress";
        public const string SemVer2StorageContainer = "semVer2StorageContainer";
        public const string SemVer2StorageKeyValue = "semVer2StorageKeyValue";
        public const string SemVer2StoragePath = "semVer2StoragePath";

        public const string UseCompressedStorage = "useCompressedStorage";
        public const string UseSemVer2Storage = "useSemVer2Storage";

        public const string ContentIsFlatContainer = "contentIsFlatContainer";
        public const string CursorUri = "cursorUri";
        public const string FlatContainerName = "flatContainerName";
        public const string AllIconsInFlatContainer = "allIconsInFlatContainer";

        #endregion

        #region Catalog2PackageFixup
        public const string Verify = "verify";
        #endregion

        #region CopyLucene
        public const string DestDirectoryType = "destDirectoryType";
        public const string DestPath = "destPath";
        public const string DestStorageAccountName = "destStorageAccountName";
        public const string DestStorageContainer = "destStorageContainer";
        public const string DestStorageKeyValue = "destStorageKeyValue";

        public const string SrcDirectoryType = "srcDirectoryType";
        public const string SrcPath = "srcPath";
        public const string SrcStorageAccountName = "srcStorageAccountName";
        public const string SrcStorageContainer = "srcStorageContainer";
        public const string SrcStorageKeyValue = "srcStorageKeyValue";

        #endregion

        #region Db2Catalog
        public const string StartDate = "startDate";
        public const string PackageContentUrlFormat = "packageContentUrlFormat";
        public const string CursorSize = "cursorSize";

        public const string StorageAccountNameAuditing = "storageAccountNameAuditing";
        public const string StorageContainerAuditing = "storageContainerAuditing";
        public const string StorageKeyValueAuditing = "storageKeyValueAuditing";
        public const string StoragePathAuditing = "storagePathAuditing";
        public const string StorageTypeAuditing = "storageTypeAuditing";
        public const string SqlCommandTimeoutInSeconds = "sqlCommandTimeoutInSeconds";

        public const string SkipCreatedPackagesProcessing = "skipCreatedPackagesProcessing";
        #endregion

        #region Monitoring
        /// <summary>
        /// The url of the service index.
        /// </summary>
        public const string Index = "index";

        /// <summary>
        /// The url of the cursor for <see cref="RegistrationEndpoint"/>.
        /// </summary>
        public const string RegistrationCursorUri = "registrationCursorUri";

        /// <summary>
        /// The url of the cursor for <see cref="FlatContainerEndpoint"/>.
        /// </summary>
        public const string FlatContainerCursorUri = "flatContainerCursorUri";

        /// <summary>
        /// The argument prefix for the cursor of a <see cref="SearchEndpoint"/>. There are multiple search endpoints
        /// so a parameter matching this prefix represents the cursor for a single instance. The suffix (after the prefix)
        /// is the instance identifier, e.g. "usnc-a".
        /// </summary>
        public const string SearchCursorUriPrefix = "searchCursorUri-";

        /// <summary>
        /// The argument prefix for the base URL of a <see cref="SearchEndpoint"/>. There should be the same number of
        /// <see cref="SearchBaseUriPrefix"/> parameters passed as <see cref="SearchCursorUriPrefix"/> with the same
        /// set of suffixes.
        /// </summary>
        public const string SearchBaseUriPrefix = "searchBaseUri-";

        /// <summary>
        /// The folder in which <see cref="PackageMonitoringStatus"/>es are saved by the <see cref="PackageMonitoringStatusService"/>.
        /// Defaults to <see cref="PackageStatusFolderDefault"/>.
        /// </summary>
        public const string PackageStatusFolder = "statusFolder";

        /// <summary>
        /// Default value of <see cref="PackageStatusFolder"/>.
        /// </summary>
        public const string PackageStatusFolderDefault = "status";

        /// <summary>
        /// If the queue contains more messages than this, the job will not requeue any invalid packages.
        /// </summary>
        public const string MaxRequeueQueueSize = "maxRequeueQueueSize";

        /// <summary>
        /// If true, packages are expected to have at least a repository signature.
        /// </summary>
        public const string RequireRepositorySignature = "requireRepositorySignature";

        /// <summary>
        /// Continue to poll for messages until this amount of time is elapsed.
        /// Then, refresh the job loop.
        /// </summary>
        public const string QueueLoopDurationHours = "queueLoopDurationHours";

        /// <summary>
        /// When the queue is empty or processing a message fails, wait this long before polling more.
        /// </summary>
        public const string QueueDelaySeconds = "queueDelaySeconds";
        #endregion

        #region KeyVault
        public const string VaultName = "vaultName";
        public const string ClientId = "clientId";

        public const string StoreName = "storeName";
        public const string StoreLocation = "storeLocation";

        public const string CertificateThumbprint = "certificateThumbprint";
        public const string ValidateCertificate = "validateCertificate";

        public const string RefreshIntervalSec = "refreshIntervalSec";
        #endregion

        #region Lightning
        public const string Command = "command";
        public const string OutputFolder = "outputFolder";
        public const string TemplateFile = "templateFile";
        public const string BatchSize = "batchSize";
        public const string IndexFile = "indexFile";
        public const string CursorFile = "cursorFile";
        #endregion
    }
}