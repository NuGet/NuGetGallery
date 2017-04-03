// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        public const int DefaultInterval = 3; // seconds
        public const string Interval = "interval";

        public const int DefaultReinitializeIntervalSec = 60*60; // 1 hour
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
        public const string StorageAccountName = "storageAccountName";
        public const string StorageBaseAddress = "storageBaseAddress";
        public const string StorageContainer = "storageContainer";
        public const string StorageKeyValue = "storageKeyValue";
        public const string StoragePath = "storagePath";
        public const string StorageType = "storageType";
        public const string Version = "version";
        #endregion

        #region Catalog2Lucene
        public const string CatalogBaseAddress = "catalogBaseAddress";
        public const string Registration = "registration";
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

        public const string UnlistShouldDelete = "unlistShouldDelete";
        public const string UseCompressedStorage = "useCompressedStorage";
        public const string UseSemVer2Storage = "useSemVer2Storage";
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

        #region Feed2Catalog
        public const string StartDate = "startDate";

        public const string StorageAccountNameAuditing = "storageAccountNameAuditing";
        public const string StorageContainerAuditing = "storageContainerAuditing";
        public const string StorageKeyValueAuditing = "storageKeyValueAuditing";
        public const string StoragePathAuditing = "storagePathAuditing";
        public const string StorageTypeAuditing = "storageTypeAuditing";
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
