// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.KeyVault;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Ng
{
    static class CommandHelpers
    {
        #region ArgumentStrings
        public const string CatalogBaseAddress = "catalogBaseAddress";
        public const string CertificateThumbprint = "certificateThumbprint";
        public const string CompressedStorageAccountName = "compressedStorageAccountName";
        public const string CompressedStorageBaseAddress = "compressedStorageBaseAddress";
        public const string CompressedStorageContainer = "compressedStorageContainer";
        public const string CompressedStorageKeyValue = "compressedStorageKeyValue";
        public const string CompressedStoragePath = "compressedStoragePath";
        public const string ConnectionString = "connectionString";
        public const string ContentBaseAddress = "contentBaseAddress";
        public const string ClientId = "clientId";
        public const string DestDirectoryType = "destDirectoryType";
        public const string DestPath = "destPath";
        public const string DestStorageAccountName = "destStorageAccountName";
        public const string DestStorageContainer = "destStorageContainer";
        public const string DestStorageKeyValue = "destStorageKeyValue";
        public const string DirectoryType = "directoryType";
        public const string Gallery = "gallery";
        public const string Id = "id";
        public const string InstrumentationKey = "instrumentationKey";
        public const string Interval = "interval";
        public const string LuceneDirectoryType = "luceneDirectoryType";
        public const string LucenePath = "lucenePath";
        public const string LuceneRegistrationTemplate = "luceneRegistrationTemplate";
        public const string LuceneReset = "luceneReset";
        public const string LuceneStorageAccountName = "luceneStorageAccountName";
        public const string LuceneStorageContainer = "luceneStorageContainer";
        public const string LuceneStorageKeyValue = "luceneStorageKeyValue";
        public const string Path = "path";
        public const string Registration = "registration";
        public const string Source = "source";
        public const string SrcDirectoryType = "srcDirectoryType";
        public const string SrcPath = "srcPath";
        public const string SrcStorageAccountName = "srcStorageAccountName";
        public const string SrcStorageContainer = "srcStorageContainer";
        public const string SrcStorageKeyValue = "srcStorageKeyValue";
        public const string StartDate = "startDate";
        public const string StorageAccountName = "storageAccountName";
        public const string StorageAccountNameAuditing = "storageAccountNameAuditing";
        public const string StorageBaseAddress = "storageBaseAddress";
        public const string StorageContainer = "storageContainer";
        public const string StorageContainerAuditing = "storageContainerAuditing";
        public const string StorageKeyValue = "storageKeyValue";
        public const string StorageKeyValueAuditing = "storageKeyValueAuditing";
        public const string StoragePath = "storagePath";
        public const string StoragePathAuditing = "storagePathAuditing";
        public const string StorageType = "storageType";
        public const string StorageTypeAuditing = "storageTypeAuditing";
        public const string VaultName = "vaultName";
        public const string ValidateCertificate = "validateCertificate";
        public const string Verbose = "verbose";
        public const string Version = "version";
        public const string UnlistShouldDelete = "unlistShouldDelete";
        public const string UseCompressedStorage = "useCompressedStorage"; 
        #endregion

        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            IDictionary<string, string> result = new Dictionary<string, string>();
            List<string> inputArgs = new List<string>();

            if (args.Length == start)
            {
                return result;
            }

            if ((args.Length - 1) % 2 != 0)
            {
                Trace.TraceError("Unexpected number of arguments");
                return null;
            }

            for (int i = 1; i < args.Length; i += 2)
            {
                result.Add(args[i], args[i + 1]);
                inputArgs.Add(args[i]);
            }

            var secretInjector = GetSecretInjector(result);

            if (secretInjector != null)
            {
                foreach (string input in inputArgs)
                {
                    result[input] = secretInjector.InjectAsync(result[input]).Result;
                }
            }

            return result;
        }

        private static void TraceRequiredArgument(string name)
        {
            Console.WriteLine("Required argument \"{0}\" not provided", name);
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }

        private static void TraceMissingArgument(string name)
        {
            Console.WriteLine("Argument \"{0}\" not provided", name);
            Trace.TraceWarning("Argument \"{0}\" not provided", name);
        }

        private static void TryGetArgument(IDictionary<string, string> arguments, string searchArg, out string value, bool required = false)
        {
            if (!arguments.TryGetValue("-" + searchArg, out value))
            {
                if (required)
                {
                    TraceRequiredArgument(searchArg);
                    throw new ArgumentException();
                }
                else
                {
                    TraceMissingArgument(searchArg);
                }

                value = null;
            }
        }

        public static SecretInjector GetSecretInjector(IDictionary<string, string> arguments)
        {
            try
            {
                string vaultName;
                TryGetArgument(arguments, VaultName, out vaultName, required: true);

                string clientId;
                TryGetArgument(arguments, ClientId, out clientId, required: true);

                string certificateThumbprint = "";
                TryGetArgument(arguments, CertificateThumbprint, out certificateThumbprint, required: true);

                string validateCertificate = "false";
                arguments.TryGetValue(ValidateCertificate, out validateCertificate);

                bool shouldValidateCertificate = validateCertificate == null
                    ? false
                    : validateCertificate.Equals("true", StringComparison.InvariantCultureIgnoreCase);

                var keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, shouldValidateCertificate);
                var keyVaultReader = new KeyVaultReader(keyVaultConfiguration);
                return new SecretInjector(keyVaultReader);
            }
            catch (ArgumentException)
            {
                return null;
            }

        }

        public static string GetSource(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Source, out value);
            return value;
        }

        public static string GetGallery(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Gallery, out value);
            return value;
        }

        public static string GetRegistration(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Registration, out value);
            return value;
        }

        public static string GetCatalogBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, CatalogBaseAddress, out value);
            return value;
        }

        public static string GetStorageBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, StorageBaseAddress, out value);
            return value;
        }

        public static string GetId(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Id, out value);
            return value;
        }

        public static string GetVersion(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Version, out value);
            return value;
        }

        public static bool GetUnlistShouldDelete(IDictionary<string, string> arguments)
        {
            return GetBool(arguments, UnlistShouldDelete, false);
        }

        public static bool GetVerbose(IDictionary<string, string> arguments)
        {
            string verboseStr = "false";
            arguments.TryGetValue("-verbose", out verboseStr);

            bool verbose = verboseStr == null ? false : verboseStr.Equals("true", StringComparison.InvariantCultureIgnoreCase);

            return verbose;
        }

        public static bool GetBool(IDictionary<string, string> arguments, string argumentName, bool defaultValue)
        {
            string argumentValue;
            TryGetArgument(arguments, argumentName, out argumentValue);
            return string.IsNullOrEmpty(argumentValue) ? defaultValue : argumentValue.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        public static int GetInterval(IDictionary<string, string> arguments)
        {
            const int DefaultInterval = 3; // seconds
            int interval = DefaultInterval;
            string intervalStr = string.Empty;
            TryGetArgument(arguments, Interval, out intervalStr);
            if (!int.TryParse(intervalStr, out interval))
            {
                interval = DefaultInterval;
            }

            return interval;
        }

        public static DateTime GetStartDate(IDictionary<string, string> arguments)
        {
            DateTime defaultStartDate = DateTime.MinValue;
            string startDateString;
            TryGetArgument(arguments, StartDate, out startDateString);
            if (!DateTime.TryParse(startDateString, out defaultStartDate))
            {
                defaultStartDate = DateTime.MinValue;
            }

            return defaultStartDate;
        }

        public static string GetLuceneRegistrationTemplate(IDictionary<string, string> arguments)
        {
            string value;
            try
            {
                TryGetArgument(arguments, LuceneRegistrationTemplate, out value, required: true);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return value;
        }

        public static string GetContentBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, ContentBaseAddress, out value);
            return value;
        }

        public static StorageFactory CreateCompressedStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            try
            {
                string useCompressedStorage = "false";
                TryGetArgument(arguments, UseCompressedStorage, out useCompressedStorage);

                if (useCompressedStorage != null && useCompressedStorage.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                Uri storageBaseAddress = null;
                string storageBaseAddressStr;
                TryGetArgument(arguments, StorageBaseAddress, out storageBaseAddressStr);
                if (!string.IsNullOrEmpty(storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageAccountName;
                TryGetArgument(arguments, CompressedStorageAccountName, out storageAccountName, required: true);

                string storageKeyValue;
                TryGetArgument(arguments, CompressedStorageKeyValue, out storageKeyValue, required: true);

                string storageContainer;
                TryGetArgument(arguments, CompressedStorageContainer, out storageContainer, required: true);

                string storagePath = null;
                TryGetArgument(arguments, CompressedStoragePath, out storagePath);

                StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                return new AzureStorageFactory(account, storageContainer, storagePath, storageBaseAddress) { Verbose = verbose, CompressContent = true };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static StorageFactory CreateStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            try
            {
                Uri storageBaseAddress = null;
                string storageBaseAddressStr;
                TryGetArgument(arguments, StorageBaseAddress, out storageBaseAddressStr);
                if (!string.IsNullOrEmpty(storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageType;
                TryGetArgument(arguments, StorageType, out storageType, required: true);

                if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
                {
                    string storagePath;
                    TryGetArgument(arguments, StoragePath, out storagePath, required: true);

                    if (storageBaseAddress == null)
                    {
                        TraceRequiredArgument("-storageBaseAddress");
                        return null;
                    }

                    return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
                }
                else if (storageType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
                {
                    string storageContainer;
                    TryGetArgument(arguments, StorageContainer, out storageContainer, required: true);

                    string storageAccountName;
                    TryGetArgument(arguments, StorageAccountName, out storageAccountName, required: true);

                    string storageKeyValue;
                    TryGetArgument(arguments, StorageKeyValue, out storageKeyValue, required: true);

                    string storagePath;
                    TryGetArgument(arguments, StoragePath, out storagePath);

                    StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                    CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                    return new AzureStorageFactory(account, storageContainer, storagePath, storageBaseAddress) { Verbose = verbose };
                }
                else
                {
                    Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                    return null;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static StorageFactory CreateSuffixedStorageFactory(string suffix, IDictionary<string, string> arguments, bool verbose)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                throw new ArgumentNullException("suffix");
            }
            try
            {
                Uri storageBaseAddress = null;
                string storageBaseAddressStr;
                TryGetArgument(arguments, StorageBaseAddress + suffix, out storageBaseAddressStr);
                if (!string.IsNullOrEmpty(storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageType;
                TryGetArgument(arguments, StorageType, out storageType, required: true);

                if (storageType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
                {
                    string storagePath;
                    TryGetArgument(arguments, StoragePath + suffix, out storagePath, required: true);

                    if (storageBaseAddress == null)
                    {
                        TraceRequiredArgument("-storageBaseAddress" + suffix);
                        return null;
                    }

                    return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
                }
                else if (storageType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
                {
                    string storageAccountName;
                    TryGetArgument(arguments, StorageAccountName + suffix, out storageAccountName, required: true);

                    string storageKeyValue;
                    TryGetArgument(arguments, StorageKeyValue + suffix, out storageKeyValue, required: true);

                    string storageContainer;
                    TryGetArgument(arguments, StorageContainer + suffix, out storageContainer, required: true);

                    string storagePath = null;
                    TryGetArgument(arguments, StoragePath + suffix, out storagePath);

                    StorageCredentials credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                    CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                    return new AzureStorageFactory(account, storageContainer, storagePath, storageBaseAddress) { Verbose = verbose };
                }
                else
                {
                    Trace.TraceError("Unrecognized storageType \"{0}\"", storageType);
                    return null;
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static bool GetLuceneReset(IDictionary<string, string> arguments)
        {
            return GetBool(arguments, LuceneReset, false);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { DirectoryType, LuceneDirectoryType },
                { Path, LucenePath },
                { StorageAccountName, LuceneStorageAccountName },
                { StorageKeyValue, LuceneStorageKeyValue },
                { StorageContainer, LuceneStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopySrcLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { DirectoryType, SrcDirectoryType },
                { Path, SrcPath },
                { StorageAccountName, SrcStorageAccountName },
                { StorageKeyValue, SrcStorageKeyValue },
                { StorageContainer, SrcStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopyDestLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { DirectoryType, DestDirectoryType },
                { Path, DestPath },
                { StorageAccountName, DestStorageAccountName },
                { StorageKeyValue, DestStorageKeyValue },
                { StorageContainer, DestStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectoryImpl(IDictionary<string, string> arguments, IDictionary<string, string> names)
        {
            try
            {
                string luceneDirectoryType;
                TryGetArgument(arguments, names[DirectoryType], out luceneDirectoryType, required: true);

                if (luceneDirectoryType.Equals("File", StringComparison.InvariantCultureIgnoreCase))
                {
                    string lucenePath;
                    TryGetArgument(arguments, names[Path], out lucenePath, required: true);

                    DirectoryInfo directoryInfo = new DirectoryInfo(lucenePath);

                    if (!directoryInfo.Exists)
                    {
                        directoryInfo.Create();
                        directoryInfo.Refresh();
                    }

                    return new SimpleFSDirectory(directoryInfo);
                }
                else if (luceneDirectoryType.Equals("Azure", StringComparison.InvariantCultureIgnoreCase))
                {
                    string luceneStorageAccountName;
                    TryGetArgument(arguments, names[StorageAccountName], out luceneStorageAccountName, required: true);

                    string luceneStorageKeyValue;
                    TryGetArgument(arguments, names[StorageKeyValue], out luceneStorageKeyValue, required: true);

                    string luceneStorageContainer;
                    TryGetArgument(arguments, names[StorageContainer], out luceneStorageContainer, required: true);

                    StorageCredentials credentials = new StorageCredentials(luceneStorageAccountName, luceneStorageKeyValue);
                    CloudStorageAccount account = new CloudStorageAccount(credentials, true);
                    return new AzureDirectory(account, luceneStorageContainer);
                }
                else
                {
                    Trace.TraceError("Unrecognized Lucene Directory Type \"{0}\"", luceneDirectoryType);
                    return null;
                }
            }
            catch(ArgumentException)
            {
                return null;
            }
        }

        public static Func<HttpMessageHandler> GetHttpMessageHandlerFactory(bool verbose, string catalogBaseAddress = null, string storageBaseAddress = null)
        {
            Func<HttpMessageHandler> handlerFunc = null;
            if (verbose)
            {
                handlerFunc = () =>
                {
                    if (catalogBaseAddress != null)
                    {
                        return new VerboseHandler(new StorageAccessHandler(catalogBaseAddress, storageBaseAddress));
                    }

                    return new VerboseHandler();
                };
            }
            return handlerFunc;
        }

        public static string GetConnectionString(IDictionary<string, string> arguments)
        {
            string connectionString;
            TryGetArgument(arguments, ConnectionString, out connectionString);
            return connectionString;
        }

        public static string Get(IDictionary<string, string> arguments, string argumentName)
        {
            string argumentValue;
            TryGetArgument(arguments, argumentName, out argumentValue);
            return argumentValue;
        }

        public static string GetPath(IDictionary<string, string> arguments)
        {
            string path;
            TryGetArgument(arguments, Path, out path);
            return path;
        }

        public static string GetApplicationInsightsInstrumentationKey(IDictionary<string, string> arguments)
        {
            string endpoint;
            if (arguments == null || !arguments.TryGetValue("-instrumentationkey", out endpoint))
            {
                return null;
            }

            return endpoint;
        }
    }
}
