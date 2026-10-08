// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Staging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class SymbolPackagePromotionValidationFacts
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesOnlyIngestionUsingImmutableSourceAndStagedAttemptKey(bool grouped)
        {
            var fixture = new Fixture();
            if (grouped)
            {
                fixture.Attempt.StagedPackageIdentity.StagingGroupKey = 7;
                fixture.Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 7, ActivePromotionId = fixture.Attempt.ActivePromotionId };
            }

            var set = await fixture.CreateSetAsync();

            Assert.Equal(fixture.Attempt.Key, set.PackageKey);
            Assert.Equal(ValidatingType.StagedSymbolPackage, set.ValidatingType);
            Assert.Equal(fixture.Message.ValidationTrackingId, set.ValidationTrackingId);
            Assert.Equal(fixture.Attempt.UploadedBlobETag, set.PackageETag);
            var ingestion = Assert.Single(set.PackageValidations);
            Assert.Equal(ValidatorName.SymbolsIngester, ingestion.Type);
            Assert.Equal(ValidationStatus.NotStarted, ingestion.ValidationStatus);
            fixture.Blobs.Verify(service => service.GetPackageReadUriAsync(fixture.Attempt.UploadedBlobPath, fixture.Attempt.UploadedBlobETag), Times.Once);
            fixture.Files.Verify(service => service.CopyPackageUrlForValidationSetAsync(set, Fixture.UploadUri.AbsoluteUri), Times.Once);
            fixture.Files.Verify(service => service.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task ExistingProcessorStartsIngestionWithoutScanOrValidation()
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            var ingestion = Assert.Single(set.PackageValidations);
            ingestion.Key = Guid.NewGuid();
            var validator = new Mock<INuGetValidator>();
            validator.Setup(service => service.GetResponseAsync(It.IsAny<INuGetValidationRequest>())).ReturnsAsync(NuGetValidationResponse.NotStarted);
            validator.Setup(service => service.StartAsync(It.IsAny<INuGetValidationRequest>())).ReturnsAsync(NuGetValidationResponse.Incomplete);
            fixture.Validators.Setup(service => service.GetNuGetValidator(ValidatorName.SymbolsIngester)).Returns(validator.Object);
            fixture.Files.Setup(service => service.GetPackageForValidationSetReadUriAsync(set, It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .ReturnsAsync(Fixture.UploadUri);
            fixture.Storage.Setup(service => service.MarkValidationStartedAsync(ingestion, It.IsAny<INuGetValidationResponse>()))
                .Callback<PackageValidation, INuGetValidationResponse>((validation, response) => validation.ValidationStatus = response.Status)
                .Returns(Task.CompletedTask);
            var processor = new ValidationSetProcessor(fixture.Validators.Object, fixture.Storage.Object, Options(fixture.Configuration),
                Options(new SasDefinitionConfiguration()), fixture.Files.Object, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<ValidationSetProcessor>>());

            await processor.ProcessValidationsAsync(set);

            validator.Verify(service => service.StartAsync(It.Is<INuGetValidationRequest>(request =>
                request.PackageKey == fixture.Attempt.Key && request.ValidationId == ingestion.Key
                && request.NupkgUrl == Fixture.UploadUri.AbsoluteUri)), Times.Once);
            Assert.Equal(ValidationStatus.Incomplete, ingestion.ValidationStatus);
            fixture.Validators.Verify(service => service.GetNuGetValidator(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OrdinarySymbolsStillRequireScanAndValidationBeforeIngestion()
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            set.ValidatingType = ValidatingType.SymbolPackage;
            set.PackageKey = fixture.Attempt.SymbolPackageKey;
            var processor = new ValidationSetProcessor(fixture.Validators.Object, fixture.Storage.Object, Options(fixture.Configuration),
                Options(new SasDefinitionConfiguration()), fixture.Files.Object, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<ValidationSetProcessor>>());

            await processor.ProcessValidationsAsync(set);

            Assert.Equal(ValidationStatus.NotStarted, Assert.Single(set.PackageValidations).ValidationStatus);
            fixture.Validators.Verify(service => service.GetNuGetValidator(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("new-promotion")]
        [InlineData("superseded")]
        [InlineData("unaccepted")]
        [InlineData("ready")]
        [InlineData("inactive-group")]
        public async Task NoSetIsCreatedForInactiveAttempt(string scenario)
        {
            var fixture = new Fixture();
            switch (scenario)
            {
                case "new-promotion":
                    fixture.Attempt.ActivePromotionId = Guid.NewGuid();
                    break;
                case "superseded":
                    fixture.Attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey++;
                    break;
                case "unaccepted":
                    fixture.Attempt.ActivePromotionId = null;
                    break;
                case "ready":
                    fixture.Attempt.Status = StagedPackageStatus.Ready;
                    break;
                case "inactive-group":
                    fixture.Attempt.StagedPackageIdentity.StagingGroupKey = 7;
                    fixture.Attempt.StagedPackageIdentity.StagingGroup = new StagingGroup { Key = 7, ActivePromotionId = Guid.NewGuid() };
                    break;
            }

            Assert.Null(await fixture.CreateSetAsync());

            fixture.Storage.Verify(service => service.GetValidationSetAsync(It.IsAny<Guid>()), Times.Never);
            fixture.Blobs.Verify(service => service.GetPackageReadUriAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OrdinarySymbolKeyCannotIdentifyStagedAttempt()
        {
            var fixture = new Fixture();
            var message = new ProcessValidationSetData("PackageA", "1.0.0",
                fixture.Message.ValidationTrackingId, ValidatingType.StagedSymbolPackage, fixture.Attempt.SymbolPackageKey);

            Assert.Null(await fixture.Provider.TryGetOrCreateValidationSetAsync(message, new StagedSymbolPackageValidatingEntity(fixture.Attempt)));

            fixture.Storage.Verify(service => service.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task PromotionCannotStartUntilExplicitlyEnabled()
        {
            var configuration = CreateOrdinaryConfiguration();
            configuration.EnableStagedSymbolPromotion = false;
            var fixture = new Fixture(configuration);

            await Assert.ThrowsAsync<NotSupportedException>(() => fixture.CreateSetAsync());

            fixture.Storage.Verify(service => service.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
        }

        [Fact]
        public async Task ValidationAndPromotionUseSeparateSetsForTheSameStagedAttempt()
        {
            var fixture = new Fixture();
            fixture.Attempt.Status = StagedPackageStatus.Validating;
            var validationMessage = new ProcessValidationSetData("PackageA", "1.0.0", Guid.NewGuid(), ValidatingType.StagedSymbolPackage, fixture.Attempt.Key);
            var entity = new StagedSymbolPackageValidatingEntity(fixture.Attempt);
            var validationSet = await fixture.Provider.TryGetOrCreateValidationSetAsync(validationMessage, entity);

            fixture.Attempt.Status = StagedPackageStatus.Promoting;
            fixture.Storage.Setup(service => service.OtherRecentValidationSetForPackageExists(
                It.IsAny<IValidatingEntity<StagedSymbolPackage>>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()))
                .ReturnsAsync((IValidatingEntity<StagedSymbolPackage> candidate, TimeSpan window, Guid trackingId) => window > TimeSpan.Zero);
            var promotionSet = await fixture.CreateSetAsync();

            Assert.Equal(validationSet.PackageKey, promotionSet.PackageKey);
            Assert.Equal(validationSet.ValidatingType, promotionSet.ValidatingType);
            Assert.NotEqual(validationSet.ValidationTrackingId, promotionSet.ValidationTrackingId);
            Assert.Equal(new[] { ValidatorName.SymbolScan, ValidatorName.SymbolsValidator }, validationSet.PackageValidations.Select(validation => validation.Type));
            Assert.Equal(ValidatorName.SymbolsIngester, Assert.Single(promotionSet.PackageValidations).Type);
            Assert.Null(await fixture.Provider.TryGetOrCreateValidationSetAsync(validationMessage, entity));
            Assert.Equal(2, validationSet.PackageValidations.Count);
        }

        [Fact]
        public async Task ExistingScanSetCannotBeReinterpretedAsPromotion()
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            set.PackageValidations.Clear();
            set.PackageValidations.Add(new PackageValidation { Type = ValidatorName.SymbolScan });
            set.PackageValidations.Add(new PackageValidation { Type = ValidatorName.SymbolsValidator });

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateSetAsync());

            Assert.Equal(2, set.PackageValidations.Count);
        }

        [Fact]
        public async Task PersistedPromotionCannotRunWhileDisabled()
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            fixture.Configuration.EnableStagedSymbolPromotion = false;
            var processor = new ValidationSetProcessor(fixture.Validators.Object, fixture.Storage.Object, Options(fixture.Configuration),
                Options(new SasDefinitionConfiguration()), fixture.Files.Object, Mock.Of<ITelemetryService>(), Mock.Of<ILogger<ValidationSetProcessor>>());

            await Assert.ThrowsAsync<NotSupportedException>(() => processor.ProcessValidationsAsync(set));

            fixture.Validators.Verify(service => service.GetNuGetValidator(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(StagedPackageStatus.Succeeded)]
        [InlineData(StagedPackageStatus.PromotionFailed)]
        public async Task TerminalPromotionRequiresExistingIngestionSetForCompletionRetry(StagedPackageStatus status)
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            fixture.Attempt.Status = status;

            Assert.Same(set, await fixture.CreateSetAsync());

            fixture.Storage.Setup(service => service.GetValidationSetAsync(It.IsAny<Guid>())).ReturnsAsync((PackageValidationSet)null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateSetAsync());

            fixture.Storage.Verify(service => service.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
        }

        [Fact]
        public async Task CurrentPromotionStillReachesOutcomeWhenParentBecomesIneligible()
        {
            var fixture = new Fixture();
            var set = await fixture.CreateSetAsync();
            fixture.Attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
            fixture.Attempt.StagedPackageIdentity.Package.PackageRegistration.Owners.Clear();

            Assert.Same(set, await fixture.CreateSetAsync());
        }

        private static ValidationConfiguration CreateOrdinaryConfiguration()
        {
            return new ValidationConfiguration
            {
                EnableStagedSymbolPromotion = true,
                Validations = new List<ValidationConfigurationItem>
                {
                    new ValidationConfigurationItem { Name = ValidatorName.SymbolScan, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                    new ValidationConfigurationItem { Name = ValidatorName.SymbolsValidator, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                    new ValidationConfigurationItem
                    {
                        Name = ValidatorName.SymbolsIngester,
                        ShouldStart = true,
                        FailureBehavior = ValidationFailureBehavior.MustSucceed,
                        RequiredValidations = new List<string> { ValidatorName.SymbolScan, ValidatorName.SymbolsValidator },
                        TrackAfter = TimeSpan.FromMinutes(5),
                    },
                },
                MissingPackageRetryCount = 3,
                NewValidationRequestDeduplicationWindow = TimeSpan.FromMinutes(5),
                ValidationMessageRecheckPeriod = TimeSpan.FromMinutes(1),
                TimeoutValidationSetAfter = TimeSpan.FromHours(1),
            };
        }

        private static IOptionsSnapshot<T> Options<T>(T value) where T : class
        {
            var options = new Mock<IOptionsSnapshot<T>>();
            options.SetupGet(accessor => accessor.Value).Returns(value);
            return options.Object;
        }

        private class Fixture
        {
            public static readonly Uri UploadUri = new Uri("https://example.test/staged-symbols");

            private readonly Dictionary<Guid, PackageValidationSet> _sets = new Dictionary<Guid, PackageValidationSet>();

            public Fixture(ValidationConfiguration configuration = null)
            {
                Configuration = configuration ?? CreateOrdinaryConfiguration();
                Attempt = new StagedSymbolPackage
                {
                    Key = 43,
                    SymbolPackageKey = 44,
                    SymbolPackage = new SymbolPackage { Key = 44, StatusKey = PackageStatus.Staged },
                    ActivePromotionId = Guid.NewGuid(),
                    Status = StagedPackageStatus.Promoting,
                    UploadedBlobPath = "symbols/43",
                    UploadedBlobETag = "etag",
                    StagedPackageIdentity = new StagedPackageIdentity
                    {
                        OwnerKey = 7,
                        CurrentStagedSymbolPackageKey = 43,
                        Package = new Package
                        {
                            NormalizedVersion = "1.0.0",
                            PackageStatusKey = PackageStatus.Available,
                            PackageRegistration = new PackageRegistration { Id = "PackageA", Owners = new[] { new User { Key = 7 } }.ToList() },
                        },
                    },
                };
                Message = new ProcessValidationSetData("PackageA", "1.0.0",
                    SymbolPromotionValidationTrackingId.Create(Attempt.ActivePromotionId.Value, Attempt.Key),
                    ValidatingType.StagedSymbolPackage, Attempt.Key);
                Storage.Setup(service => service.GetValidationSetAsync(It.IsAny<Guid>()))
                    .ReturnsAsync((Guid trackingId) => _sets.TryGetValue(trackingId, out var set) ? set : null);
                Storage.Setup(service => service.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                    .ReturnsAsync((PackageValidationSet set) => _sets[set.ValidationTrackingId] = set);
                Blobs.Setup(service => service.GetPackageReadUriAsync(Attempt.UploadedBlobPath, Attempt.UploadedBlobETag)).ReturnsAsync(UploadUri);
                Provider = new StagedSymbolPackageValidationSetProvider(Storage.Object, Files.Object, Blobs.Object, Validators.Object,
                    Options(Configuration), Options(new SasDefinitionConfiguration()), Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger<ValidationSetProvider<StagedSymbolPackage>>>());
            }

            public StagedSymbolPackage Attempt { get; }

            public ProcessValidationSetData Message { get; }

            public ValidationConfiguration Configuration { get; }

            public Mock<IValidationStorageService> Storage { get; } = new Mock<IValidationStorageService>();

            public Mock<IValidationFileService> Files { get; } = new Mock<IValidationFileService>();

            public Mock<IStagingBlobService> Blobs { get; } = new Mock<IStagingBlobService>();

            public Mock<IValidatorProvider> Validators { get; } = new Mock<IValidatorProvider>();

            public StagedSymbolPackageValidationSetProvider Provider { get; }

            public Task<PackageValidationSet> CreateSetAsync()
            {
                return Provider.TryGetOrCreateValidationSetAsync(Message, new StagedSymbolPackageValidatingEntity(Attempt));
            }
        }
    }
}
