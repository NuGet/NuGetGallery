// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class Db2AzureSearchCommandFacts
    {
        private readonly Mock<INewPackageRegistrationProducer> _producer;
        private readonly Mock<IIndexActionBuilder> _builder;
        private readonly Mock<ISearchServiceClientWrapper> _serviceClient;
        private readonly Mock<IIndexesOperationsWrapper> _serviceClientIndexes;
        private readonly Mock<IBatchPusher> _batchPusher;
        private readonly Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>> _options;
        private readonly Db2AzureSearchConfiguration _config;
        private readonly RecordingLogger<Db2AzureSearchCommand> _logger;
        private readonly Db2AzureSearchCommand _target;

        public Db2AzureSearchCommandFacts(ITestOutputHelper output)
        {
            _producer = new Mock<INewPackageRegistrationProducer>();
            _builder = new Mock<IIndexActionBuilder>();
            _serviceClient = new Mock<ISearchServiceClientWrapper>();
            _serviceClientIndexes = new Mock<IIndexesOperationsWrapper>();
            _batchPusher = new Mock<IBatchPusher>();
            _options = new Mock<IOptionsSnapshot<Db2AzureSearchConfiguration>>();
            _logger = output.GetLogger<Db2AzureSearchCommand>();

            _config = new Db2AzureSearchConfiguration
            {
                SearchIndexName = "search",
                HijackIndexName = "hijack",
                MaxConcurrentBatches = 1,
            };

            _options
                .Setup(x => x.Value)
                .Returns(() => _config);
            _serviceClient
                .Setup(x => x.Indexes)
                .Returns(() => _serviceClientIndexes.Object);
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.IsAny<NewPackageRegistration>()))
                .Returns(() => new IndexActions(
                    new IndexAction<KeyedDocument>[0],
                    new IndexAction<KeyedDocument>[0],
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));

            _target = new Db2AzureSearchCommand(
                _producer.Object,
                _builder.Object,
                _serviceClient.Object,
                () => _batchPusher.Object,
                _options.Object,
                _logger);
        }

        [Fact]
        public async Task ReplacesIndexes()
        {
            _config.ReplaceIndexes = true;
            _serviceClientIndexes
                .Setup(x => x.ExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            await _target.ExecuteAsync();

            _serviceClientIndexes.Verify(
                x => x.ExistsAsync(_config.SearchIndexName),
                Times.Once);
            _serviceClientIndexes.Verify(
                x => x.ExistsAsync(_config.HijackIndexName),
                Times.Once);
            _serviceClientIndexes.Verify(
                x => x.DeleteAsync(_config.SearchIndexName),
                Times.Once);
            _serviceClientIndexes.Verify(
                x => x.DeleteAsync(_config.HijackIndexName),
                Times.Once);
            _serviceClientIndexes.Verify(
                x => x.CreateAsync(It.Is<Index>(i => i.Name == _config.SearchIndexName)),
                Times.Once);
            _serviceClientIndexes.Verify(
                x => x.CreateAsync(It.Is<Index>(i => i.Name == _config.HijackIndexName)),
                Times.Once);
        }

        [Fact]
        public async Task PushesToIndexesUsingMaximumBatchSize()
        {
            _config.AzureSearchBatchSize = 2;
            _producer
                .Setup(x => x.ProduceWorkAsync(It.IsAny<ConcurrentBag<NewPackageRegistration>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<ConcurrentBag<NewPackageRegistration>, CancellationToken>((w, _) =>
                {
                    w.Add(new NewPackageRegistration("A", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("B", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("C", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("D", 0, new string[0], new Package[0]));
                    w.Add(new NewPackageRegistration("E", 0, new string[0], new Package[0]));
                });
            _builder
                .Setup(x => x.AddNewPackageRegistration(It.IsAny<NewPackageRegistration>()))
                .Returns<NewPackageRegistration>(x => new IndexActions(
                    new List<IndexAction<KeyedDocument>> { IndexAction.Upload(new KeyedDocument { Key = x.PackageId }) },
                    new List<IndexAction<KeyedDocument>>(),
                    new ResultAndAccessCondition<VersionListData>(
                        new VersionListData(new Dictionary<string, VersionPropertiesData>()),
                        AccessConditionWrapper.GenerateEmptyCondition())));

            var enqueuedIndexActions = new List<KeyValuePair<string, IndexActions>>();
            _batchPusher
                .Setup(x => x.EnqueueIndexActions(It.IsAny<string>(), It.IsAny<IndexActions>()))
                .Callback<string, IndexActions>((id, actions) =>
                {
                    enqueuedIndexActions.Add(KeyValuePair.Create(id, actions));
                });

            await _target.ExecuteAsync();

            Assert.Equal(5, enqueuedIndexActions.Count);
            var keys = enqueuedIndexActions
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();
            Assert.Equal(
                new[] { "A", "B", "C", "D", "E" },
                keys);

            _batchPusher.Verify(x => x.PushFullBatchesAsync(), Times.Exactly(5));
            _batchPusher.Verify(x => x.FinishAsync(), Times.Once);
        }
    }
}
