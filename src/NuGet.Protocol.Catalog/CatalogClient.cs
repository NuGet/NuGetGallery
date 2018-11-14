// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGet.Protocol.Catalog
{
    public class CatalogClient : ICatalogClient
    {
        private static readonly JsonSerializer JsonSerializer = CatalogJsonSerialization.Serializer;
        private readonly HttpClient _httpClient;
        private readonly ILogger<CatalogClient> _logger;

        public CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<CatalogIndex> GetIndexAsync(string indexUrl)
        {
            return DeserializeUrlAsync<CatalogIndex>(indexUrl);
        }

        public Task<CatalogPage> GetPageAsync(string pageUrl)
        {
            return DeserializeUrlAsync<CatalogPage>(pageUrl);
        }

        public async Task<CatalogLeaf> GetLeafAsync(string leafUrl)
        {
            // Buffer all of the JSON so we can parse twice. Once to determine the leaf type and once to deserialize
            // the entire thing to the proper leaf type.
            _logger.LogDebug("Downloading {leafUrl} as a byte array.", leafUrl);
            var jsonBytes = await _httpClient.GetByteArrayAsync(leafUrl);
            var untypedLeaf = DeserializeBytes<CatalogLeaf>(jsonBytes);

            switch (untypedLeaf.Type)
            {
                case CatalogLeafType.PackageDetails:
                    return DeserializeBytes<PackageDetailsCatalogLeaf>(jsonBytes);
                case CatalogLeafType.PackageDelete:
                    return DeserializeBytes<PackageDeleteCatalogLeaf>(jsonBytes);
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
            var leaf = await DeserializeUrlAsync<T>(leafUrl);

            if (leaf.Type != type)
            {
                throw new ArgumentException(
                    $"The leaf type found in the document does not match the expected '{type}' type.",
                    nameof(type));
            }

            return leaf;
        }

        private T DeserializeBytes<T>(byte[] jsonBytes)
        {
            using (var stream = new MemoryStream(jsonBytes))
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JsonSerializer.Deserialize<T>(jsonReader);
            }
        }

        private async Task<T> DeserializeUrlAsync<T>(string documentUrl)
        {
            _logger.LogDebug("Downloading {documentUrl} as a stream.", documentUrl);

            using (var stream = await _httpClient.GetStreamAsync(documentUrl))
            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JsonSerializer.Deserialize<T>(jsonReader);
            }
        }
    }
}
