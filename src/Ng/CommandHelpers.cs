// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;
using CatalogAggregateStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.AggregateStorageFactory;
using CatalogAzureStorage = NuGet.Services.Metadata.Catalog.Persistence.AzureStorage;
using CatalogAzureStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.AzureStorageFactory;
using CatalogFileStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.FileStorageFactory;
using CatalogStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.StorageFactory;
using ICatalogStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.IStorageFactory;

namespace Ng
{
    public static class CommandHelpers
    {
        public static IDictionary<string, string> GetArguments(string[] args, int start)
        {
            var unprocessedArguments = new Dictionary<string, string>();

            if ((args.Length - 1) % 2 != 0)
            {
                Trace.TraceError("Unexpected number of arguments");
                return null;
            }

            for (var i = start; i < args.Length; i += 2)
            {
                // Remove hyphen from the beginning of the argument name.
                var argumentName = args[i].TrimStart(Arguments.Prefix);
                // Remove quotes (if any) from the start and end of the argument value.
                var argumentValue = args[i + 1].Trim(Arguments.Quote);
                unprocessedArguments.Add(argumentName, argumentValue);
            }

            var secretInjector = GetSecretInjector(unprocessedArguments);

            return new SecretDictionary(secretInjector, unprocessedArguments);
        }

        private static void TraceRequiredArgument(string name)
        {
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }

        private static ISecretInjector GetSecretInjector(IDictionary<string, string> arguments)
        {
            ISecretReader secretReader;

            var vaultName = arguments.GetOrDefault<string>(Arguments.VaultName);
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = arguments.GetOrThrow<string>(Arguments.ClientId);
                var certificateThumbprint = arguments.GetOrThrow<string>(Arguments.CertificateThumbprint);
                var storeName = arguments.GetOrDefault(Arguments.StoreName, StoreName.My);
                var storeLocation = arguments.GetOrDefault(Arguments.StoreLocation, StoreLocation.LocalMachine);
                var shouldValidateCert = arguments.GetOrDefault(Arguments.ValidateCertificate, true);

                var keyVaultCertificate = CertificateUtility.FindCertificateByThumbprint(storeName, storeLocation, certificateThumbprint, shouldValidateCert);
                var keyVaultConfig = new KeyVaultConfiguration(vaultName, clientId, keyVaultCertificate);

                secretReader = new CachingSecretReader(new KeyVaultReader(keyVaultConfig),
                    arguments.GetOrDefault(Arguments.RefreshIntervalSec, CachingSecretReader.DefaultRefreshIntervalSec));
            }

            return new SecretInjector(secretReader);
        }

        public static void AssertAzureStorage(IDictionary<string, string> arguments)
        {
            if (arguments.GetOrThrow<string>(Arguments.StorageType) != Arguments.AzureStorageType)
            {
                throw new ArgumentException("Only Azure storage is supported!");
            }
        }

        public static RegistrationStorageFactories CreateRegistrationStorageFactories(IDictionary<string, string> arguments, bool verbose)
        {
            CatalogStorageFactory legacyStorageFactory;
            var semVer2StorageFactory = CreateSemVer2StorageFactory(arguments, verbose);

            var storageFactory = CreateStorageFactory(arguments, verbose);
            var compressedStorageFactory = CreateCompressedStorageFactory(arguments, verbose);
            if (compressedStorageFactory != null)
            {
                var secondaryStorageBaseUrlRewriter = new SecondaryStorageBaseUrlRewriter(new List<KeyValuePair<string, string>>
                {
                    // always rewrite storage root url in seconary
                    new KeyValuePair<string, string>(storageFactory.BaseAddress.ToString(), compressedStorageFactory.BaseAddress.ToString())
                });

                var aggregateStorageFactory = new CatalogAggregateStorageFactory(
                    storageFactory,
                    new[] { compressedStorageFactory },
                    secondaryStorageBaseUrlRewriter.Rewrite,
                    verbose);

                legacyStorageFactory = aggregateStorageFactory;
            }
            else
            {
                legacyStorageFactory = storageFactory;
            }

            return new RegistrationStorageFactories(legacyStorageFactory, semVer2StorageFactory);
        }

        public static CatalogStorageFactory CreateStorageFactory(
            IDictionary<string, string> arguments,
            bool verbose,
            IThrottle throttle = null)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.StorageBaseAddress, Arguments.StorageBaseAddress },
                { Arguments.StorageAccountName, Arguments.StorageAccountName },
                { Arguments.StorageKeyValue, Arguments.StorageKeyValue },
                { Arguments.StorageContainer, Arguments.StorageContainer },
                { Arguments.StoragePath, Arguments.StoragePath },
                { Arguments.StorageSuffix, Arguments.StorageSuffix },
                { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy },
                { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds },
                { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds }
            };

            return CreateStorageFactoryImpl(
                arguments,
                names,
                verbose,
                compressed: false,
                throttle: throttle);
        }

        public static CatalogStorageFactory CreateCompressedStorageFactory(IDictionary<string, string> arguments, bool verbose)
        {
            if (!arguments.GetOrDefault(Arguments.UseCompressedStorage, false))
            {
                return null;
            }

            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.StorageBaseAddress, Arguments.CompressedStorageBaseAddress },
                { Arguments.StorageAccountName, Arguments.CompressedStorageAccountName },
                { Arguments.StorageKeyValue, Arguments.CompressedStorageKeyValue },
                { Arguments.StorageContainer, Arguments.CompressedStorageContainer },
                { Arguments.StoragePath, Arguments.CompressedStoragePath },
                { Arguments.StorageSuffix, Arguments.StorageSuffix },
                { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy },
                { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds },
                { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds }
            };

            return CreateStorageFactoryImpl(arguments, names, verbose, compressed: true);
        }

        public static CatalogStorageFactory CreateSemVer2StorageFactory(
            IDictionary<string, string> arguments,
            bool verbose)
        {
            if (!arguments.GetOrDefault(Arguments.UseSemVer2Storage, false))
            {
                return null;
            }

            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.StorageBaseAddress, Arguments.SemVer2StorageBaseAddress },
                { Arguments.StorageAccountName, Arguments.SemVer2StorageAccountName },
                { Arguments.StorageKeyValue, Arguments.SemVer2StorageKeyValue },
                { Arguments.StorageContainer, Arguments.SemVer2StorageContainer },
                { Arguments.StoragePath, Arguments.SemVer2StoragePath },
                { Arguments.StorageSuffix, Arguments.StorageSuffix },
                { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy },
                { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds },
                { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds }
            };

            return CreateStorageFactoryImpl(arguments, names, verbose, compressed: true);
        }

        public static CatalogStorageFactory CreateSuffixedStorageFactory(
            string suffix,
            IDictionary<string, string> arguments,
            bool verbose,
            IThrottle throttle = null)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                throw new ArgumentNullException(nameof(suffix));
            }

            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.StorageBaseAddress, Arguments.StorageBaseAddress + suffix },
                { Arguments.StorageAccountName, Arguments.StorageAccountName + suffix },
                { Arguments.StorageKeyValue, Arguments.StorageKeyValue + suffix },
                { Arguments.StorageContainer, Arguments.StorageContainer + suffix },
                { Arguments.StoragePath, Arguments.StoragePath + suffix },
                { Arguments.StorageSuffix, Arguments.StorageSuffix + suffix },
                { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy + suffix },
                { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds + suffix },
                { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds }
            };

            return CreateStorageFactoryImpl(
                arguments,
                names,
                verbose,
                compressed: false,
                throttle: throttle);
        }

        private static CatalogStorageFactory CreateStorageFactoryImpl(
            IDictionary<string, string> arguments,
            IDictionary<string, string> argumentNameMap,
            bool verbose,
            bool compressed,
            IThrottle throttle = null)
        {
            Uri storageBaseAddress = null;
            var storageBaseAddressStr = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StorageBaseAddress]);
            if (!string.IsNullOrEmpty(storageBaseAddressStr))
            {
                storageBaseAddressStr = storageBaseAddressStr.TrimEnd('/') + "/";

                storageBaseAddress = new Uri(storageBaseAddressStr);
            }

            var storageType = arguments.GetOrThrow<string>(Arguments.StorageType);

            if (storageType.Equals(Arguments.FileStorageType, StringComparison.InvariantCultureIgnoreCase))
            {
                var storagePath = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StoragePath]);

                if (storageBaseAddress != null)
                {
                    return new CatalogFileStorageFactory(storageBaseAddress, storagePath, verbose);
                }

                TraceRequiredArgument(argumentNameMap[Arguments.StorageBaseAddress]);
                return null;
            }

            if (Arguments.AzureStorageType.Equals(storageType, StringComparison.InvariantCultureIgnoreCase))
            {
                var storageAccountName = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageAccountName]);
                var storageKeyValue = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageKeyValue]);
                var storageContainer = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageContainer]);
                var storagePath = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StoragePath]);
                var storageSuffix = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StorageSuffix]);
                var storageOperationMaxExecutionTime = MaxExecutionTime(arguments.GetOrDefault<int>(argumentNameMap[Arguments.StorageOperationMaxExecutionTimeInSeconds]));
                var storageServerTimeout = MaxExecutionTime(arguments.GetOrDefault<int>(argumentNameMap[Arguments.StorageServerTimeoutInSeconds]));
                var storageUseServerSideCopy = arguments.GetOrDefault<bool>(argumentNameMap[Arguments.StorageUseServerSideCopy]);

                var credentials = new StorageCredentials(storageAccountName, storageKeyValue);

                var account = string.IsNullOrEmpty(storageSuffix) ?
                    new CloudStorageAccount(credentials, useHttps: true) :
                    new CloudStorageAccount(credentials, storageSuffix, useHttps: true);

                return new CatalogAzureStorageFactory(
                    account,
                    storageContainer,
                    storageOperationMaxExecutionTime,
                    storageServerTimeout,
                    storagePath,
                    storageBaseAddress,
                    storageUseServerSideCopy,
                    compressed,
                    verbose,
                    initializeContainer: true,
                    throttle: throttle ?? NullThrottle.Instance);
            }
            throw new ArgumentException($"Unrecognized storageType \"{storageType}\"");
        }

        private static TimeSpan MaxExecutionTime(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentException($"{nameof(seconds)} cannot be negative.");
            }
            if (seconds == 0)
            {
                return CatalogAzureStorage.DefaultMaxExecutionTime;
            }
            return TimeSpan.FromSeconds(seconds);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(
            IDictionary<string, string> arguments,
            bool required = true)
        {
            return GetLuceneDirectory(arguments, out var destination, required);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectory(
            IDictionary<string, string> arguments,
            out string destination,
            bool required = true)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.DirectoryType, Arguments.LuceneDirectoryType },
                { Arguments.Path, Arguments.LucenePath },
                { Arguments.StorageAccountName, Arguments.LuceneStorageAccountName },
                { Arguments.StorageKeyValue, Arguments.LuceneStorageKeyValue },
                { Arguments.StorageContainer, Arguments.LuceneStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names, out destination, required);
        }

        public static Lucene.Net.Store.Directory GetCopySrcLuceneDirectory(IDictionary<string, string> arguments, bool required = true)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.DirectoryType, Arguments.SrcDirectoryType },
                { Arguments.Path, Arguments.SrcPath },
                { Arguments.StorageAccountName, Arguments.SrcStorageAccountName },
                { Arguments.StorageKeyValue, Arguments.SrcStorageKeyValue },
                { Arguments.StorageContainer, Arguments.SrcStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names, out var destination, required);
        }

        public static Lucene.Net.Store.Directory GetCopyDestLuceneDirectory(IDictionary<string, string> arguments, bool required = true)
        {
            IDictionary<string, string> names = new Dictionary<string, string>
            {
                { Arguments.DirectoryType, Arguments.DestDirectoryType },
                { Arguments.Path, Arguments.DestPath },
                { Arguments.StorageAccountName, Arguments.DestStorageAccountName },
                { Arguments.StorageKeyValue, Arguments.DestStorageKeyValue },
                { Arguments.StorageContainer, Arguments.DestStorageContainer }
            };

            return GetLuceneDirectoryImpl(arguments, names, out var destination, required);
        }

        public static Lucene.Net.Store.Directory GetLuceneDirectoryImpl(
            IDictionary<string, string> arguments,
            IDictionary<string, string> argumentNameMap,
            out string destination,
            bool required = true)
        {
            destination = null;

            try
            {
                var luceneDirectoryType = arguments.GetOrThrow<string>(argumentNameMap[Arguments.DirectoryType]);

                if (luceneDirectoryType.Equals(Arguments.FileStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    var lucenePath = arguments.GetOrThrow<string>(argumentNameMap[Arguments.Path]);

                    var directoryInfo = new DirectoryInfo(lucenePath);

                    destination = lucenePath;

                    if (directoryInfo.Exists)
                    {
                        return new SimpleFSDirectory(directoryInfo);
                    }

                    directoryInfo.Create();
                    directoryInfo.Refresh();

                    return new SimpleFSDirectory(directoryInfo);
                }
                if (luceneDirectoryType.Equals(Arguments.AzureStorageType, StringComparison.InvariantCultureIgnoreCase))
                {
                    var luceneStorageAccountName = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageAccountName]);

                    var luceneStorageKeyValue = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageKeyValue]);

                    var luceneStorageContainer = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageContainer]);

                    var credentials = new StorageCredentials(luceneStorageAccountName, luceneStorageKeyValue);
                    var account = new CloudStorageAccount(credentials, useHttps: true);

                    destination = luceneStorageContainer;

                    return new AzureDirectory(account, luceneStorageContainer);
                }
                Trace.TraceError("Unrecognized Lucene Directory Type \"{0}\"", luceneDirectoryType);
                return null;
            }
            catch (ArgumentException)
            {
                if (required)
                {
                    throw;
                }

                return null;
            }
        }

        public static Func<HttpMessageHandler> GetHttpMessageHandlerFactory(
            ITelemetryService telemetryService,
            bool verbose,
            string catalogBaseAddress = null,
            string storageBaseAddress = null)
        {
            Func<HttpMessageHandler> defaultHandlerFunc = () =>
            {
                var httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };

                return new TelemetryHandler(telemetryService, httpClientHandler);
            };

            Func<HttpMessageHandler> handlerFunc = defaultHandlerFunc;

            if (verbose)
            {
                handlerFunc =
                    () =>
                        catalogBaseAddress != null
                            ? new VerboseHandler(new StorageAccessHandler(catalogBaseAddress, storageBaseAddress, defaultHandlerFunc()))
                            : new VerboseHandler(defaultHandlerFunc());
            }

            return handlerFunc;
        }

        public static EndpointConfiguration GetEndpointConfiguration(IDictionary<string, string> arguments)
        {
            var registrationCursorUri = arguments.GetOrThrow<Uri>(Arguments.RegistrationCursorUri);
            var flatContainerCursorUri = arguments.GetOrThrow<Uri>(Arguments.FlatContainerCursorUri);

            var instanceNameToSearchBaseUri = GetSuffixToUri(arguments, Arguments.SearchBaseUriPrefix);
            var instanceNameToSearchCursorUri = GetSuffixToUri(arguments, Arguments.SearchCursorUriPrefix);
            var instanceNameToSearchConfig = new Dictionary<string, SearchEndpointConfiguration>();
            foreach (var pair in instanceNameToSearchBaseUri)
            {
                var instanceName = pair.Key;

                // Find all cursors with an instance name starting with the search base URI instance name. We do this
                // because there may be multiple potential cursors representing the state of a search service.
                var matchingCursors = instanceNameToSearchCursorUri.Keys.Where(x => x.StartsWith(instanceName)).ToList();

                if (!matchingCursors.Any())
                {
                    throw new ArgumentException(
                        $"The -{Arguments.SearchBaseUriPrefix}{instanceName} argument does not have any matching " +
                        $"-{Arguments.SearchCursorUriPrefix}{instanceName}* arguments.");
                }

                instanceNameToSearchConfig[instanceName] = new SearchEndpointConfiguration(
                    matchingCursors.Select(x => instanceNameToSearchCursorUri[x]).ToList(),
                    pair.Value);

                foreach (var key in matchingCursors)
                {
                    instanceNameToSearchCursorUri.Remove(key);
                }
            }
            
            // See if there are any search cursor URI arguments left over and error out. Better to fail than to ignore
            // an argument that the user expected to be relevant.
            if (instanceNameToSearchCursorUri.Any())
            {
                throw new ArgumentException(
                    $"There are -{Arguments.SearchCursorUriPrefix}* arguments without matching " +
                    $"-{Arguments.SearchBaseUriPrefix}* arguments. The unmatched suffixes were: {string.Join(", ", instanceNameToSearchCursorUri.Keys)}");
            }

            return new EndpointConfiguration(
                registrationCursorUri,
                flatContainerCursorUri,
                instanceNameToSearchConfig);
        }

        private static Dictionary<string, Uri> GetSuffixToUri(IDictionary<string, string> arguments, string prefix)
        {
            var suffixToUri = new Dictionary<string, Uri>();
            foreach (var key in arguments.Keys.Where(x => x.StartsWith(prefix)))
            {
                var suffix = key.Substring(prefix.Length);
                suffixToUri[suffix] = arguments.GetOrThrow<Uri>(key);
            }

            return suffixToUri;
        }

        public static IPackageMonitoringStatusService GetPackageMonitoringStatusService(IDictionary<string, string> arguments, ICatalogStorageFactory storageFactory, ILoggerFactory loggerFactory)
        {
            return new PackageMonitoringStatusService(
                new NamedStorageFactory(storageFactory, arguments.GetOrDefault(Arguments.PackageStatusFolder, Arguments.PackageStatusFolderDefault)),
                loggerFactory.CreateLogger<PackageMonitoringStatusService>());
        }

        public static IStorageQueue<T> CreateStorageQueue<T>(IDictionary<string, string> arguments, int version)
        {
            var storageType = arguments.GetOrThrow<string>(Arguments.StorageType);

            if (Arguments.AzureStorageType.Equals(storageType, StringComparison.InvariantCultureIgnoreCase))
            {
                var storageAccountName = arguments.GetOrThrow<string>(Arguments.StorageAccountName);
                var storageKeyValue = arguments.GetOrThrow<string>(Arguments.StorageKeyValue);
                var storageQueueName = arguments.GetOrDefault<string>(Arguments.StorageQueueName);

                var credentials = new StorageCredentials(storageAccountName, storageKeyValue);
                var account = new CloudStorageAccount(credentials, true);
                return new StorageQueue<T>(new AzureStorageQueue(account, storageQueueName),
                    new JsonMessageSerializer<T>(JsonSerializerUtility.SerializerSettings), version);
            }
            else
            {
                throw new NotImplementedException("Only Azure storage queues are supported!");
            }
        }
    }
}