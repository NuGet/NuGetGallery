// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.ScoringProfiles;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch
{
    public class IndexBuilder : IIndexBuilder
    {
        private readonly ISearchIndexClientWrapper _searchIndexClient;
        private readonly IOptionsSnapshot<AzureSearchJobConfiguration> _options;
        private readonly ILogger<IndexBuilder> _logger;

        public IndexBuilder(
            ISearchIndexClientWrapper searchIndexClient,
            IOptionsSnapshot<AzureSearchJobConfiguration> options,
            ILogger<IndexBuilder> logger)
        {
            _searchIndexClient = searchIndexClient ?? throw new ArgumentNullException(nameof(searchIndexClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateSearchIndexAsync()
        {
            await CreateIndexAsync(InitializeSearchIndex());
        }

        public async Task CreateHijackIndexAsync()
        {
            await CreateIndexAsync(InitializeHijackIndex());
        }

        public async Task CreateSearchIndexIfNotExistsAsync()
        {
            await CreateIndexIfNotExistsAsync(InitializeSearchIndex());
        }

        public async Task CreateHijackIndexIfNotExistsAsync()
        {
            await CreateIndexIfNotExistsAsync(InitializeHijackIndex());
        }

        public async Task DeleteSearchIndexIfExistsAsync()
        {
            await DeleteIndexIfExistsAsync(_options.Value.SearchIndexName);
        }

        public async Task DeleteHijackIndexIfExistsAsync()
        {
            await DeleteIndexIfExistsAsync(_options.Value.HijackIndexName);
        }

        private async Task DeleteIndexIfExistsAsync(string indexName)
        {
            if (await IndexExistsAsync(indexName))
            {
                _logger.LogWarning("Deleting index {IndexName}.", indexName);
                await _searchIndexClient.DeleteIndexAsync(indexName);
                _logger.LogWarning("Done deleting index {IndexName}.", indexName);
            }
            else
            {
                _logger.LogInformation("Skipping the deletion of index {IndexName} since it does not exist.", indexName);
            }
        }

        private async Task CreateIndexAsync(SearchIndex index)
        {
            _logger.LogInformation("Creating index {IndexName}.", index.Name);
            await _searchIndexClient.CreateIndexAsync(index);
            _logger.LogInformation("Done creating index {IndexName}.", index.Name);
        }

        private async Task CreateIndexIfNotExistsAsync(SearchIndex index)
        {
            if (!(await IndexExistsAsync(index.Name)))
            {
                await CreateIndexAsync(index);
            }
            else
            {
                _logger.LogInformation("Skipping the creation of index {IndexName} since it already exists.", index.Name);
            }
        }

        private SearchIndex InitializeSearchIndex()
        {
            return InitializeIndex<SearchDocument.Full>(
                _options.Value.SearchIndexName, addScoringProfile: true);
        }

        private SearchIndex InitializeHijackIndex()
        {
            return InitializeIndex<HijackDocument.Full>(
                _options.Value.HijackIndexName, addScoringProfile: false);
        }

        private async Task<bool> IndexExistsAsync(string name)
        {
            try
            {
                await _searchIndexClient.GetIndexAsync(name);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public static JsonObjectSerializer GetJsonSerializer()
        {
            return new JsonObjectSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { KeyedDocumentConverter.Instance },
            });
        }

        private SearchIndex InitializeIndex<TDocument>(string name, bool addScoringProfile)
        {
            var fieldBuilder = new FieldBuilder();
            fieldBuilder.Serializer = GetJsonSerializer();

            var index = new SearchIndex(name)
            {
                Fields = fieldBuilder.Build(typeof(TDocument)),
                Analyzers =
                {
                    DescriptionAnalyzer.Instance,
                    ExactMatchCustomAnalyzer.Instance,
                    PackageIdCustomAnalyzer.Instance,
                    TagsCustomAnalyzer.Instance
                },
                Tokenizers =
                {
                    PackageIdCustomTokenizer.Instance,
                },
                TokenFilters =
                {
                    IdentifierCustomTokenFilter.Instance,
                    TruncateCustomTokenFilter.Instance,
                },
            };

            if (addScoringProfile)
            {
                var scoringProfile = DefaultScoringProfile.Create(_options.Value.Scoring);

                index.ScoringProfiles.Add(scoringProfile);
                index.DefaultScoringProfile = DefaultScoringProfile.Name;
            }

            return index;
        }
    }
}
