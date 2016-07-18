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
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            var result = new Dictionary<string, string>();
            var inputArgs = new List<string>();

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

            foreach (string input in inputArgs)
            {
                result[input] = secretInjector.InjectAsync(result[input]).Result;
            }

            return result;
        }

        private static void TraceRequiredArgument(string name)
        {
            Console.WriteLine("Required argument \"{0}\" not provided", name);
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }

        private static bool TryGetArgument(IDictionary<string, string> arguments, string searchArg, out string value, bool required = false)
        {
            if (!arguments.TryGetValue(Constants.ArgumentPrefix + searchArg, out value))
            {
                if (required)
                {
                    TraceRequiredArgument(searchArg);
                    throw new ArgumentException("Required argument not provided", searchArg);
                }

                value = null;
                return false;
            }

            return true;
        }

        private static SecretInjector GetSecretInjector(IDictionary<string, string> arguments)
        {
            ISecretReader secretReader;
            string vaultName;
            if (!TryGetArgument(arguments, Constants.VaultName, out vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                string clientId;
                TryGetArgument(arguments, Constants.ClientId, out clientId, required: true);

                string certificateThumbprint;
                TryGetArgument(arguments, Constants.CertificateThumbprint, out certificateThumbprint, required: true);

                bool shouldValidateCertificate = GetBool(arguments, Constants.ValidateCertificate, defaultValue: false);

                var keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, shouldValidateCertificate);
                secretReader = new KeyVaultReader(keyVaultConfiguration);
            }

            return new SecretInjector(secretReader);
        }

        public static string GetSource(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.Source, out value);
            return value;
        }

        public static string GetGallery(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.Gallery, out value);
            return value;
        }

        public static string GetRegistration(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.Registration, out value);
            return value;
        }

        public static string GetCatalogBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.CatalogBaseAddress, out value);
            return value;
        }

        public static string GetStorageBaseAddress(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.StorageBaseAddress, out value);
            return value;
        }

        public static string GetId(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.Id, out value);
            return value;
        }

        public static string GetVersion(IDictionary<string, string> arguments)
        {
            string value;
            TryGetArgument(arguments, Constants.Version, out value);
            return value;
        }

        public static bool GetUnlistShouldDelete(IDictionary<string, string> arguments)
        {
            return GetBool(arguments, Constants.UnlistShouldDelete, defaultValue: false);
        }

        public static bool GetVerbose(IDictionary<string, string> arguments)
        {
            return GetBool(arguments, Constants.Verbose, defaultValue: false);
        }

        public static bool GetBool(IDictionary<string, string> arguments, string argumentName, bool defaultValue)
        {
            bool result;
            string argumentValue;
            if (TryGetArgument(arguments, argumentName, out argumentValue))
            {
                result = bool.Parse(argumentValue);
            }
            else
            {
                result = defaultValue;
            }

            return result;
        }

        public static int GetInterval(IDictionary<string, string> arguments, int defaultInterval)
        {
            int interval = defaultInterval;
            string intervalStr = string.Empty;
            TryGetArgument(arguments, Constants.Interval, out intervalStr);
            if (!int.TryParse(intervalStr, out interval))
            {
                interval = defaultInterval;
            }

            return interval;
        }

        public static DateTime GetStartDate(IDictionary<string, string> arguments)
        {
            DateTime defaultStartDate = DateTime.MinValue;
            string startDateString;
            TryGetArgument(arguments, Constants.StartDate, out startDateString);
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
                TryGetArgument(arguments, Constants.LuceneRegistrationTemplate, out value, required: true);
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
            TryGetArgument(arguments, Constants.ContentBaseAddress, out value);
            return value;
        }

        public static StorageFactory CreateCompressedStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            try
            {
                string useCompressedStorage = "false";
                TryGetArgument(arguments, Constants.UseCompressedStorage, out useCompressedStorage);

                if (useCompressedStorage != null && useCompressedStorage.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                Uri storageBaseAddress = null;
                string storageBaseAddressStr;
                if (!TryGetArgument(arguments, Constants.StorageBaseAddress, out storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageAccountName;
                TryGetArgument(arguments, Constants.CompressedStorageAccountName, out storageAccountName, required: true);

                string storageKeyValue;
                TryGetArgument(arguments, Constants.CompressedStorageKeyValue, out storageKeyValue, required: true);

                string storageContainer;
                TryGetArgument(arguments, Constants.CompressedStorageContainer, out storageContainer, required: true);

                string storagePath = null;
                TryGetArgument(arguments, Constants.CompressedStoragePath, out storagePath);

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
                if (!TryGetArgument(arguments, Constants.StorageBaseAddress, out storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageType;
                TryGetArgument(arguments, Constants.StorageType, out storageType, required: true);

                if (storageType.Equals(Constants.FileStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string storagePath;
                    TryGetArgument(arguments, Constants.StoragePath, out storagePath, required: true);

                    if (storageBaseAddress == null)
                    {
                        TraceRequiredArgument(Constants.StorageBaseAddress);
                        return null;
                    }

                    return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
                }
                else if (storageType.Equals(Constants.AzureStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string storageContainer;
                    TryGetArgument(arguments, Constants.StorageContainer, out storageContainer, required: true);

                    string storageAccountName;
                    TryGetArgument(arguments, Constants.StorageAccountName, out storageAccountName, required: true);

                    string storageKeyValue;
                    TryGetArgument(arguments, Constants.StorageKeyValue, out storageKeyValue, required: true);

                    string storagePath;
                    TryGetArgument(arguments, Constants.StoragePath, out storagePath);

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
                TryGetArgument(arguments, Constants.StorageBaseAddress + suffix, out storageBaseAddressStr);
                if (!string.IsNullOrEmpty(storageBaseAddressStr))
                {
                    storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                    storageBaseAddress = new Uri(storageBaseAddressStr);
                }

                string storageType;
                TryGetArgument(arguments, Constants.StorageType, out storageType, required: true);

                if (storageType.Equals(Constants.FileStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string storagePath;
                    TryGetArgument(arguments, Constants.StoragePath + suffix, out storagePath, required: true);

                    if (storageBaseAddress == null)
                    {
                        TraceRequiredArgument(Constants.StorageBaseAddress + suffix);
                        return null;
                    }

                    return new FileStorageFactory(storageBaseAddress, storagePath) { Verbose = verbose };
                }
                else if (storageType.Equals(Constants.AzureStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string storageAccountName;
                    TryGetArgument(arguments, Constants.StorageAccountName + suffix, out storageAccountName, required: true);

                    string storageKeyValue;
                    TryGetArgument(arguments, Constants.StorageKeyValue + suffix, out storageKeyValue, required: true);

                    string storageContainer;
                    TryGetArgument(arguments, Constants.StorageContainer + suffix, out storageContainer, required: true);

                    string storagePath = null;
                    TryGetArgument(arguments, Constants.StoragePath + suffix, out storagePath);

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
            return GetBool(arguments, Constants.LuceneReset, defaultValue: false);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Constants.DirectoryType, Constants.LuceneDirectoryType },
                { Constants.Path, Constants.LucenePath },
                { Constants.StorageAccountName, Constants.LuceneStorageAccountName },
                { Constants.StorageKeyValue, Constants.LuceneStorageKeyValue },
                { Constants.StorageContainer, Constants.LuceneStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopySrcLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Constants.DirectoryType, Constants.SrcDirectoryType },
                { Constants.Path, Constants.SrcPath },
                { Constants.StorageAccountName, Constants.SrcStorageAccountName },
                { Constants.StorageKeyValue, Constants.SrcStorageKeyValue },
                { Constants.StorageContainer, Constants.SrcStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetCopyDestLuceneDirectory(IDictionary<string, string> arguments)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Constants.DirectoryType, Constants.DestDirectoryType },
                { Constants.Path, Constants.DestPath },
                { Constants.StorageAccountName, Constants.DestStorageAccountName },
                { Constants.StorageKeyValue, Constants.DestStorageKeyValue },
                { Constants.StorageContainer, Constants.DestStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectoryImpl(IDictionary<string, string> arguments, IDictionary<string, string> names)
        {
            try
            {
                string luceneDirectoryType;
                TryGetArgument(arguments, names[Constants.DirectoryType], out luceneDirectoryType, required: true);

                if (luceneDirectoryType.Equals(Constants.FileStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string lucenePath;
                    TryGetArgument(arguments, names[Constants.Path], out lucenePath, required: true);

                    DirectoryInfo directoryInfo = new DirectoryInfo(lucenePath);

                    if (!directoryInfo.Exists)
                    {
                        directoryInfo.Create();
                        directoryInfo.Refresh();
                    }

                    return new SimpleFSDirectory(directoryInfo);
                }
                else if (luceneDirectoryType.Equals(Constants.AzureStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    string luceneStorageAccountName;
                    TryGetArgument(arguments, names[Constants.StorageAccountName], out luceneStorageAccountName, required: true);

                    string luceneStorageKeyValue;
                    TryGetArgument(arguments, names[Constants.StorageKeyValue], out luceneStorageKeyValue, required: true);

                    string luceneStorageContainer;
                    TryGetArgument(arguments, names[Constants.StorageContainer], out luceneStorageContainer, required: true);

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
            catch (ArgumentException)
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
            TryGetArgument(arguments, Constants.ConnectionString, out connectionString);
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
            TryGetArgument(arguments, Constants.Path, out path);
            return path;
        }

        public static string GetApplicationInsightsInstrumentationKey(IDictionary<string, string> arguments)
        {
            string endpoint;
            TryGetArgument(arguments, Constants.InstrumentationKey, out endpoint);
            return endpoint;
        }
    }
}
