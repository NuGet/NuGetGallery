// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator;
using NuGetGallery;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using Moq;
using Xunit;


namespace NuGet.Services.Validation.Symbols
{
    public class SymbolMessageEnqueuerFacts
    {
        [Fact]
        public async Task SendsSerializeMessage()
        {
            SymbolsValidatorMessage message = null;
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object)
                .Callback<SymbolsValidatorMessage>(x => message = x);

            await _target.EnqueueSymbolsValidationMessageAsync(_validationRequest.Object);

            Assert.Equal(_validationRequest.Object.ValidationId, message.ValidationId);
            Assert.Equal(_validationRequest.Object.PackageId, message.PackageId);
            Assert.Equal(_validationRequest.Object.PackageVersion, message.PackageNormalizedVersion);

            Assert.Equal(_validationRequest.Object.NupkgUrl, message.SnupkgUrl);
            Assert.Null(message.ParentPackageUrl);
            _serializer.Verify(
                x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()),
                Times.Once);
            _topicClient.Verify(x => x.SendAsync(_brokeredMessage.Object), Times.Once);
            _topicClient.Verify(x => x.SendAsync(It.IsAny<IBrokeredMessage>()), Times.Once);
        }

        [Fact]
        public async Task SendsExactStagedParentUrl()
        {
            var identity = new StagedPackageIdentity { Package = new Package { PackageStatusKey = PackageStatus.Staged }, CurrentStagedPackageKey = 50, CurrentStagedSymbolPackageKey = 43 };
            identity.CurrentStagedPackage = new StagedPackage { Key = 50, UploadedBlobPath = "exact-parent.nupkg", UploadedBlobETag = "parent-etag" };
            var attempt = new StagedSymbolPackage { Key = 43, StagedPackageIdentity = identity, Status = StagedPackageStatus.Validating };
            _validationRequest.Setup(x => x.PackageKey).Returns(attempt.Key);
            var storage = new Mock<IValidationStorageService>();
            storage.Setup(x => x.TryGetParentValidationSetAsync(_validationRequest.Object.ValidationId)).ReturnsAsync(new PackageValidationSet { PackageKey = attempt.Key, ValidatingType = ValidatingType.StagedSymbolPackage });
            var entities = new Mock<IEntityService<StagedSymbolPackage>>();
            entities.Setup(x => x.FindPackageByKey(attempt.Key)).Returns(new StagedSymbolPackageValidatingEntity(attempt));
            var blobs = new Mock<IStagingBlobService>();
            var uri = new Uri("https://example.test/exact-parent.nupkg");
            blobs.Setup(x => x.GetPackageReadUriAsync("exact-parent.nupkg", "parent-etag")).ReturnsAsync(uri);
            SymbolsValidatorMessage message = null;
            _serializer.Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>())).Callback<SymbolsValidatorMessage>(value => message = value).Returns(_brokeredMessage.Object);
            var target = new SymbolsMessageEnqueuer(_topicClient.Object, _serializer.Object, null, storage.Object, entities.Object, blobs.Object);

            await target.EnqueueSymbolsValidationMessageAsync(_validationRequest.Object);

            Assert.Equal(uri.AbsoluteUri, message.ParentPackageUrl);
            blobs.Verify(x => x.GetPackageReadUriAsync("exact-parent.nupkg", "parent-etag"), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("https://example.test/private-parent.nupkg")]
        public void SerializerPreservesPrivateParentAndSupportsLegacyMessages(string parentPackageUrl)
        {
            var message = new SymbolsValidatorMessage(Guid.NewGuid(), 43, "PackageA", "1.0.0", "https://example.test/symbols.snupkg", parentPackageUrl);
            var serializer = new SymbolsValidatorMessageSerializer();
            var serialized = serializer.Serialize(message);
            var body = JObject.Parse(serialized.GetBody());
            if (parentPackageUrl == null)
            {
                body.Remove(nameof(SymbolsValidatorMessage.ParentPackageUrl));
            }

            var received = new Mock<IReceivedBrokeredMessage>();
            received.SetupGet(x => x.Properties).Returns(serialized.Properties.ToDictionary(pair => pair.Key, pair => pair.Value));
            received.Setup(x => x.GetBody()).Returns(body.ToString());

            var result = serializer.Deserialize(received.Object);

            Assert.Equal(message.ValidationId, result.ValidationId);
            Assert.Equal(message.SnupkgUrl, result.SnupkgUrl);
            Assert.Equal(parentPackageUrl, result.ParentPackageUrl);
        }

        private readonly Mock<ITopicClient> _topicClient;
        private readonly Mock<IBrokeredMessageSerializer<SymbolsValidatorMessage>> _serializer;
        private readonly SymbolsValidationConfiguration _configuration;
        private readonly Mock<IBrokeredMessage> _brokeredMessage;
        private readonly Mock<INuGetValidationRequest> _validationRequest;
        private readonly SymbolsMessageEnqueuer _target;

        public SymbolMessageEnqueuerFacts()
        {
            _configuration = new SymbolsValidationConfiguration();
            _brokeredMessage = new Mock<IBrokeredMessage>();
            _validationRequest = new Mock<INuGetValidationRequest>();

            _validationRequest.Setup(x => x.ValidationId).Returns(new Guid("ab2629ce-2d67-403a-9a42-49748772ae90"));
            _validationRequest.Setup(x => x.PackageId).Returns("NuGet.Versioning");
            _validationRequest.Setup(x => x.PackageVersion).Returns("4.6.0");
            _validationRequest.Setup(x => x.NupkgUrl).Returns("http://example/nuget.versioning.4.6.0.nupkg?my-sas");
            _brokeredMessage.SetupProperty(x => x.ScheduledEnqueueTimeUtc);

            _topicClient = new Mock<ITopicClient>();

            _serializer = new Mock<IBrokeredMessageSerializer<SymbolsValidatorMessage>>();
            _serializer
                .Setup(x => x.Serialize(It.IsAny<SymbolsValidatorMessage>()))
                .Returns(() => _brokeredMessage.Object);

            var storage = new Mock<IValidationStorageService>();
            storage.Setup(x => x.TryGetParentValidationSetAsync(_validationRequest.Object.ValidationId)).ReturnsAsync(new PackageValidationSet { PackageKey = _validationRequest.Object.PackageKey, ValidatingType = ValidatingType.SymbolPackage });
            _target = new SymbolsMessageEnqueuer(
                _topicClient.Object,
                _serializer.Object,
                TimeSpan.FromSeconds(1),
                storage.Object,
                Mock.Of<IEntityService<StagedSymbolPackage>>(),
                Mock.Of<IStagingBlobService>());
        }
    }
}
