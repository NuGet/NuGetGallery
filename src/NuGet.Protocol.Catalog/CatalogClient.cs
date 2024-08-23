// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Protocol.Catalog
{
    public class CatalogClient : ICatalogClient
    {
        private readonly ISimpleHttpClient _jsonClient;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(ISimpleHttpClient jsonClient, ILogger<CatalogClient> logger)
        {
            _jsonClient = jsonClient ?? throw new ArgumentNullException(nameof(jsonClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CatalogIndex> GetIndexAsync(string indexUrl)
        {
            var result = await _jsonClient.DeserializeUrlAsync<CatalogIndex>(indexUrl);
            return result.GetResultOrThrow();
        }

        public async Task<CatalogPage> GetPageAsync(string pageUrl)
        {
            var result = await _jsonClient.DeserializeUrlAsync<CatalogPage>(pageUrl);
            return result.GetResultOrThrow();
        }

        public async Task<CatalogLeaf> GetLeafAsync(string leafUrl)
        {
            // Buffer all of the JSON so we can parse twice. Once to determine the leaf type and once to deserialize
            // the entire thing to the proper leaf type.
            _logger.LogDebug("Downloading {leafUrl} as a byte array.", leafUrl);
            var jsonBytes = await _jsonClient.GetByteArrayAsync(leafUrl);
            var untypedLeaf = _jsonClient.DeserializeBytes<CatalogLeaf>(jsonBytes);

            switch (untypedLeaf.Type)
            {
                case CatalogLeafType.PackageDetails:
                    return _jsonClient.DeserializeBytes<PackageDetailsCatalogLeaf>(jsonBytes);
                case CatalogLeafType.PackageDelete:
                    return _jsonClient.DeserializeBytes<PackageDeleteCatalogLeaf>(jsonBytes);
                default:
                    throw new NotSupportedException($"The catalog leaf type '{untypedLeaf.Type}' is not supported.");
            }
        }

        private async Task<CatalogLeaf> GetLeafAsync(CatalogLeafType type, string leafUrl)
        {
            switch (type)
            {
                case CatalogLeafType.PackageDetails:
                    return await GetPackageDetailsLeafAsync(leafUrl);
                case CatalogLeafType.PackageDelete:
                    return await GetPackageDeleteLeafAsync(leafUrl);
                default:
                    throw new NotSupportedException($"The catalog leaf type '{type}' is not supported.");
            }
        }

        public Task<PackageDeleteCatalogLeaf> GetPackageDeleteLeafAsync(string leafUrl)
        {
            return GetAndValidateLeafAsync<PackageDeleteCatalogLeaf>(CatalogLeafType.PackageDelete, leafUrl);
        }

        public Task<PackageDetailsCatalogLeaf> GetPackageDetailsLeafAsync(string leafUrl)
        {
            return GetAndValidateLeafAsync<PackageDetailsCatalogLeaf>(CatalogLeafType.PackageDetails, leafUrl);
        }

        private async Task<T> GetAndValidateLeafAsync<T>(CatalogLeafType type, string leafUrl) where T : CatalogLeaf
        {
            var result = await _jsonClient.DeserializeUrlAsync<T>(leafUrl);
            var leaf = result.GetResultOrThrow();

            if (leaf.Type != type)
            {
                throw new ArgumentException(
                    $"The leaf type found in the document does not match the expected '{type}' type.",
                    nameof(type));
            }

            return leaf;
        }
    }
}
