// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;
using CatalogAzureStorage = NuGet.Services.Metadata.Catalog.Persistence.AzureStorage;
using CatalogAzureStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.AzureStorageFactory;
using CatalogFileStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.FileStorageFactory;
using CatalogStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.StorageFactory;
using ICatalogStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.IStorageFactory;

namespace Ng
{
    public static class CommandHelpers
    {
        private static readonly int DefaultKeyVaultSecretCachingTimeout = 60 * 60 * 6; // 6 hours;
        private static readonly HashSet<string> NotInjectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "connectionString",
        };
        private static readonly IDictionary<string, string> ArgumentNames = new Dictionary<string, string>
        {
            { Arguments.StorageBaseAddress, Arguments.StorageBaseAddress },
            { Arguments.StorageAccountName, Arguments.StorageAccountName },
            { Arguments.StorageKeyValue, Arguments.StorageKeyValue },
            { Arguments.StorageSasValue, Arguments.StorageSasValue },
            { Arguments.StorageContainer, Arguments.StorageContainer },
            { Arguments.StoragePath, Arguments.StoragePath },
            { Arguments.StorageSuffix, Arguments.StorageSuffix },
            { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy },
            { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds },
            { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds },
            { Arguments.StorageInitializeContainer, Arguments.StorageInitializeContainer },
        };

        public static IDictionary<string, string> GetArguments(string[] args, int start, out ICachingSecretInjector secretInjector)
        {
            var unprocessedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if ((args.Length - 1) % 2 != 0)
            {
                Trace.TraceError("Unexpected number of arguments");
                secretInjector = null;

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

            secretInjector = GetSecretInjector(unprocessedArguments);

            return new SecretDictionary(secretInjector, unprocessedArguments, NotInjectedKeys);
        }

        private static void TraceRequiredArgument(string name)
        {
            Trace.TraceError("Required argument \"{0}\" not provided", name);
        }

        private static ICachingSecretInjector GetSecretInjector(IDictionary<string, string> arguments)
        {
            ICachingSecretReader secretReader;

            var vaultName = arguments.GetOrDefault<string>(Arguments.VaultName);
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var useManagedIdentity = arguments.GetOrDefault<bool>(Arguments.UseManagedIdentity);
                KeyVaultConfiguration keyVaultConfig;
                if (useManagedIdentity)
                {
                    var clientId = arguments.GetOrDefault<string>(Arguments.ClientId);
                    keyVaultConfig = new KeyVaultConfiguration(vaultName, clientId);
                }
                else
                {
                    var tenantId = arguments.GetOrThrow<string>(Arguments.TenantId);
                    var clientId = arguments.GetOrThrow<string>(Arguments.ClientId);
                    var certificateThumbprint = arguments.GetOrThrow<string>(Arguments.CertificateThumbprint);
                    var storeName = arguments.GetOrDefault(Arguments.StoreName, StoreName.My);
                    var storeLocation = arguments.GetOrDefault(Arguments.StoreLocation, StoreLocation.LocalMachine);
                    var shouldValidateCert = arguments.GetOrDefault(Arguments.ValidateCertificate, defaultValue: true);
                    var sendX5c = arguments.GetOrDefault(Arguments.SendX5c, defaultValue: false);

                    var keyVaultCertificate = CertificateUtility.FindCertificateByThumbprint(storeName, storeLocation, certificateThumbprint, shouldValidateCert);
                    keyVaultConfig = new KeyVaultConfiguration(
                        vaultName,
                        tenantId,
                        clientId, 
                        keyVaultCertificate,
                        sendX5c);
                }

                secretReader = new CachingSecretReader(new KeyVaultReader(keyVaultConfig),
                    arguments.GetOrDefault(Arguments.RefreshIntervalSec, DefaultKeyVaultSecretCachingTimeout));
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

        public static CatalogStorageFactory CreateStorageFactory(
            IDictionary<string, string> arguments,
            bool verbose,
            IThrottle throttle = null)
        {
            return CreateStorageFactoryImpl(
                arguments,
                ArgumentNames,
                verbose,
                compressed: false,
                throttle: throttle);
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
                { Arguments.StorageSasValue, Arguments.StorageSasValue + suffix },
                { Arguments.StorageContainer, Arguments.StorageContainer + suffix },
                { Arguments.StoragePath, Arguments.StoragePath + suffix },
                { Arguments.StorageSuffix, Arguments.StorageSuffix + suffix },
                { Arguments.StorageUseServerSideCopy, Arguments.StorageUseServerSideCopy + suffix },
                { Arguments.StorageOperationMaxExecutionTimeInSeconds, Arguments.StorageOperationMaxExecutionTimeInSeconds + suffix },
                { Arguments.StorageServerTimeoutInSeconds, Arguments.StorageServerTimeoutInSeconds },
                { Arguments.StorageInitializeContainer, Arguments.StorageInitializeContainer + suffix },
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
                var storageContainer = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageContainer]);
                var storagePath = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StoragePath]);
                var storageSuffix = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StorageSuffix]);
                var storageOperationMaxExecutionTime = MaxExecutionTime(arguments.GetOrDefault<int>(argumentNameMap[Arguments.StorageOperationMaxExecutionTimeInSeconds]));
                var storageServerTimeout = MaxExecutionTime(arguments.GetOrDefault<int>(argumentNameMap[Arguments.StorageServerTimeoutInSeconds]));
                var storageUseServerSideCopy = arguments.GetOrDefault<bool>(argumentNameMap[Arguments.StorageUseServerSideCopy]);
                var storageInitializeContainer = arguments.GetOrDefault<bool>(argumentNameMap[Arguments.StorageInitializeContainer], defaultValue: true);

                BlobServiceClient account = GetBlobServiceClient(storageAccountName, storageSuffix, arguments, argumentNameMap);

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
                    storageInitializeContainer,
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
                var storageQueueName = arguments.GetOrDefault<string>(Arguments.StorageQueueName);

                QueueServiceClient account = GetQueueServiceClient(storageAccountName, endpointSuffix: null, arguments, ArgumentNames);
                return new StorageQueue<T>(new AzureStorageQueue(account, storageQueueName),
                    new JsonMessageSerializer<T>(JsonSerializerUtility.SerializerSettings), version);
            }
            else
            {
                throw new NotImplementedException("Only Azure storage queues are supported!");
            }
        }

        private static BlobServiceClient GetBlobServiceClient(string storageAccountName, string endpointSuffix, IDictionary<string, string> arguments, IDictionary<string, string> argumentNameMap)
        {
            var storageKeyValue = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StorageKeyValue]);

            string connectionString;

            if (string.IsNullOrEmpty(storageKeyValue))
            {
                var storageSasValue = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageSasValue]);
                connectionString = $"BlobEndpoint=https://{storageAccountName}.blob.core.windows.net/;SharedAccessSignature={storageSasValue}";
            }
            else
            {
                connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageKeyValue};EndpointSuffix={endpointSuffix}";
            }

            return new BlobServiceClient(connectionString);
        }

        private static QueueServiceClient GetQueueServiceClient(string storageAccountName, string endpointSuffix, IDictionary<string, string> arguments, IDictionary<string, string> argumentNameMap)
        {
            var storageKeyValue = arguments.GetOrDefault<string>(argumentNameMap[Arguments.StorageKeyValue]);

            string connectionString;

            if (string.IsNullOrEmpty(storageKeyValue))
            {
                var storageSasValue = arguments.GetOrThrow<string>(argumentNameMap[Arguments.StorageSasValue]);
                connectionString = $"BlobEndpoint=https://{storageAccountName}.blob.core.windows.net/;SharedAccessSignature={storageSasValue}";
            }
            else
            {
                connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageKeyValue};EndpointSuffix={endpointSuffix}";
            }

            return new QueueServiceClient(connectionString);
        }

    }
}