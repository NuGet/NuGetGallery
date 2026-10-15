// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Entities;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.Symbols;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class StagedSymbolPackageValidationFacts
    {
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task DoesNotCreateValidationSetForWrongAttemptOrType(bool current, bool stagedType)
        {
            var attempt = CreateAttempt();
            attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey = current ? attempt.Key : 99;
            var fileService = new Mock<IValidationFileService>(MockBehavior.Strict);
            var blobService = new Mock<IStagingBlobService>(MockBehavior.Strict);
            var provider = new TestProvider(fileService.Object, blobService.Object);
            var message = new ProcessValidationSetData("PackageA", "1.0.0",
                Guid.NewGuid(), stagedType ? ValidatingType.StagedSymbolPackage : ValidatingType.SymbolPackage, attempt.Key);

            var result = await provider.TryGetOrCreateValidationSetAsync(message, new StagedSymbolPackageValidatingEntity(attempt));

            Assert.Null(result);
        }

        [Fact]
        public async Task DoesNotValidateSymbolsAgainstUnavailableParent()
        {
            var attempt = CreateAttempt();
            attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Validating;
            var provider = new TestProvider(
                new Mock<IValidationFileService>(MockBehavior.Strict).Object,
                new Mock<IStagingBlobService>(MockBehavior.Strict).Object);
            var message = new ProcessValidationSetData("PackageA", "1.0.0",
                Guid.NewGuid(), ValidatingType.StagedSymbolPackage, attempt.Key);

            Assert.Null(await provider.TryGetOrCreateValidationSetAsync(message, new StagedSymbolPackageValidatingEntity(attempt)));
        }

        [Fact]
        public async Task ExistingSetMustMatchImmutableBlobETag()
        {
            var attempt = CreateAttempt();
            var trackingId = Guid.NewGuid();
            var storage = new Mock<IValidationStorageService>();
            var existingSet = new PackageValidationSet
            {
                ValidationTrackingId = trackingId,
                PackageKey = attempt.Key,
                PackageETag = "different",
                ValidatingType = ValidatingType.StagedSymbolPackage,
            };
            storage.Setup(service => service.GetValidationSetAsync(trackingId)).ReturnsAsync(existingSet);
            var provider = new TestProvider(Mock.Of<IValidationFileService>(), Mock.Of<IStagingBlobService>(), storage.Object);
            var message = new ProcessValidationSetData("PackageA", "1.0.0",
                trackingId, ValidatingType.StagedSymbolPackage, attempt.Key);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.TryGetOrCreateValidationSetAsync(message, new StagedSymbolPackageValidatingEntity(attempt)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesValidationSetWithStagedSymbolValidators(bool stagedParent)
        {
            var attempt = CreateAttempt();
            if (stagedParent)
            {
                attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Staged;
                attempt.StagedPackageIdentity.CurrentStagedPackageKey = 50;
            }

            var newId = Guid.NewGuid();
            var storage = new Mock<IValidationStorageService>();
            storage.Setup(x => x.CreateValidationSetAsync(It.IsAny<PackageValidationSet>())).ReturnsAsync((PackageValidationSet set) => set);
            storage.Setup(x => x.OtherRecentValidationSetForPackageExists(
                It.IsAny<IValidatingEntity<StagedSymbolPackage>>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>())).ReturnsAsync(false);
            var uri = new Uri("https://example.test/staged-symbols");
            var files = new Mock<IValidationFileService>();
            files.Setup(x => x.CopyPackageUrlForValidationSetAsync(It.IsAny<PackageValidationSet>(), uri.AbsoluteUri)).Returns(Task.CompletedTask);
            var blobs = new Mock<IStagingBlobService>();
            blobs.Setup(x => x.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag)).ReturnsAsync(uri);
            var provider = new TestProvider(files.Object, blobs.Object, storage.Object);
            var message = new ProcessValidationSetData("PackageA", "1.0.0", newId, ValidatingType.StagedSymbolPackage, attempt.Key);

            var result = await provider.TryGetOrCreateValidationSetAsync(message, new StagedSymbolPackageValidatingEntity(attempt));

            Assert.Equal(newId, result.ValidationTrackingId);
            Assert.Equal(attempt.UploadedBlobETag, result.PackageETag);
            Assert.Equal(new[] { ValidatorName.SymbolScan, ValidatorName.SymbolsValidator },
                result.PackageValidations.Select(v => v.Type).ToArray());
            blobs.Verify(x => x.GetPackageReadUriAsync(attempt.UploadedBlobPath, attempt.UploadedBlobETag), Times.Once);
            files.Verify(x => x.CopyPackageUrlForValidationSetAsync(result, uri.AbsoluteUri), Times.Once);
            storage.Verify(x => x.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            storage.Verify(x => x.OtherRecentValidationSetForPackageExists(
                It.IsAny<IValidatingEntity<StagedSymbolPackage>>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public void RequiresBothStagedSymbolValidators()
        {
            var provider = new TestProvider(Mock.Of<IValidationFileService>(), Mock.Of<IStagingBlobService>(),
                configuration: new ValidationConfiguration
                {
                    Validations = new List<ValidationConfigurationItem>
                    {
                        new ValidationConfigurationItem { Name = ValidatorName.SymbolsValidator, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                        new ValidationConfigurationItem { Name = ValidatorName.SymbolsIngester, ShouldStart = true },
                    },
                });

            Assert.Throws<InvalidOperationException>(() => provider.GetValidationNames(CreateAttempt()));
        }

        [Theory]
        [InlineData(44, "etag", true, true)]
        [InlineData(43, "different", true, true)]
        [InlineData(43, "etag", false, true)]
        [InlineData(43, "etag", true, false)]
        public async Task IneligibleOutcomeDoesNotUpdateAttempt(int packageKey, string etag, bool current, bool parentAvailable)
        {
            var attempt = CreateAttempt();
            attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey = current ? attempt.Key : 99;
            attempt.StagedPackageIdentity.Package.PackageStatusKey = parentAvailable ? PackageStatus.Available : PackageStatus.Validating;
            var service = new Mock<IEntityService<StagedSymbolPackage>>(MockBehavior.Strict);
            var processor = new StagedSymbolPackageStatusProcessor(service.Object);
            var set = new PackageValidationSet
            {
                PackageKey = packageKey,
                PackageETag = etag,
                ValidationTrackingId = Guid.NewGuid(),
                ValidatingType = ValidatingType.StagedSymbolPackage,
            };

            await processor.SetStatusAsync(new StagedSymbolPackageValidatingEntity(attempt), set, PackageStatus.Available);

            service.Verify(x => x.UpdateStatusAsync(It.IsAny<StagedSymbolPackage>(), It.IsAny<PackageStatus>(), It.IsAny<bool>()), Times.Never);
        }

        [Theory]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.FailedValidation, false)]
        [InlineData(PackageStatus.Available, true)]
        [InlineData(PackageStatus.FailedValidation, true)]
        public async Task CurrentOutcomeOnlyUpdatesStagedAttempt(PackageStatus status, bool stagedParent)
        {
            var attempt = CreateAttempt();
            if (stagedParent)
            {
                attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Staged;
                attempt.StagedPackageIdentity.CurrentStagedPackageKey = 50;
            }

            var service = new Mock<IEntityService<StagedSymbolPackage>>();
            var set = new PackageValidationSet
            {
                PackageKey = attempt.Key,
                PackageETag = attempt.UploadedBlobETag,
                ValidationTrackingId = Guid.NewGuid(),
                ValidatingType = ValidatingType.StagedSymbolPackage,
            };
            var processor = new StagedSymbolPackageStatusProcessor(service.Object);

            await processor.SetStatusAsync(new StagedSymbolPackageValidatingEntity(attempt), set, status);

            service.Verify(x => x.UpdateStatusAsync(attempt, status, true), Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available, StagedPackageStatus.Ready)]
        [InlineData(PackageStatus.FailedValidation, StagedPackageStatus.FailedValidation)]
        public async Task EntityServiceChangesOnlyStagedStatus(PackageStatus status, StagedPackageStatus expected)
        {
            var attempt = CreateAttempt();
            attempt.SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged };
            var context = new Mock<IEntitiesContext>();
            context.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            var service = new StagedSymbolPackageEntityService(context.Object);

            await service.UpdateStatusAsync(attempt, status);

            Assert.Equal(expected, attempt.Status);
            Assert.Equal(PackageStatus.Staged, attempt.SymbolPackage.StatusKey);
            context.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available)]
        [InlineData(PackageStatus.Staged)]
        public void EntityLookupRequiresAccessibleParentAndCurrentAttempt(PackageStatus parentStatus)
        {
            var attempt = CreateAttempt();
            attempt.StagedPackageIdentity.Package.PackageStatusKey = parentStatus;
            attempt.StagedPackageIdentity.Package.NormalizedVersion = "1.0.0";
            attempt.StagedPackageIdentity.Package.PackageRegistration = new PackageRegistration { Id = "PackageA" };
            var query = new[] { attempt }.AsQueryable();
            var set = new Mock<DbSet<StagedSymbolPackage>>();
            set.As<IQueryable<StagedSymbolPackage>>().Setup(x => x.Provider).Returns(query.Provider);
            set.As<IQueryable<StagedSymbolPackage>>().Setup(x => x.Expression).Returns(query.Expression);
            set.As<IQueryable<StagedSymbolPackage>>().Setup(x => x.ElementType).Returns(query.ElementType);
            set.As<IQueryable<StagedSymbolPackage>>().Setup(x => x.GetEnumerator()).Returns(() => query.GetEnumerator());
            set.Setup(x => x.Include(It.IsAny<string>())).Returns(set.Object);
            var context = new Mock<IEntitiesContext>();
            context.SetupGet(x => x.StagedSymbolPackages).Returns(set.Object);
            var service = new StagedSymbolPackageEntityService(context.Object);

            Assert.Equal(attempt.Key, service.FindPackageByIdAndVersionStrict("PackageA", "1.0.0").Key);
            attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Validating;
            Assert.Null(service.FindPackageByIdAndVersionStrict("PackageA", "1.0.0"));
            attempt.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
            attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey = 99;
            Assert.Null(service.FindPackageByIdAndVersionStrict("PackageA", "1.0.0"));
        }

        [Fact]
        public async Task SymbolsValidatorTracksStagedType()
        {
            var state = new ValidatorStatus { State = ValidationStatus.NotStarted, ValidatorIssues = new List<ValidatorIssue>() };
            var stateService = new Mock<IValidatorStateService>();
            stateService.Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>())).ReturnsAsync(state);
            stateService.Setup(x => x.TryAddValidatorStatusAsync(It.IsAny<INuGetValidationRequest>(), state, ValidationStatus.Incomplete))
                .ReturnsAsync(new ValidatorStatus { State = ValidationStatus.Incomplete, ValidatorIssues = new List<ValidatorIssue>() });
            var storage = new Mock<IValidationStorageService>();
            storage.Setup(x => x.TryGetParentValidationSetAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new PackageValidationSet { PackageKey = 43, ValidatingType = ValidatingType.StagedSymbolPackage });
            var validator = new SymbolsValidator(stateService.Object, Mock.Of<ISymbolsMessageEnqueuer>(),
                Mock.Of<ITelemetryService>(), Mock.Of<ILogger<SymbolsValidator>>(), storage.Object);

            await validator.StartAsync(new NuGetValidationRequest(Guid.NewGuid(), 43, "PackageA", "1.0.0", "https://example.test/symbols"));

            Assert.Equal(ValidatingType.StagedSymbolPackage, state.ValidatingType);
        }

        [Fact]
        public async Task SymbolScanUsesSymbolPackageOfStagedAttempt()
        {
            var symbol = new SymbolPackage();
            var attempt = CreateAttempt();
            attempt.SymbolPackage = symbol;
            var entityService = new Mock<IEntityService<StagedSymbolPackage>>();
            entityService.Setup(x => x.FindPackageByKey(attempt.Key)).Returns(new StagedSymbolPackageValidatingEntity(attempt));
            var state = new ValidatorStatus { State = ValidationStatus.NotStarted, ValidatorIssues = new List<ValidatorIssue>() };
            var stateService = new Mock<IValidatorStateService>();
            stateService.Setup(x => x.GetStatusAsync(It.IsAny<INuGetValidationRequest>())).ReturnsAsync(state);
            var storage = new Mock<IValidationStorageService>();
            storage.Setup(x => x.TryGetParentValidationSetAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new PackageValidationSet { PackageKey = attempt.Key, ValidatingType = ValidatingType.StagedSymbolPackage });
            var evaluator = new Mock<ICriteriaEvaluator<SymbolPackage>>();
            evaluator.Setup(x => x.IsMatch(It.IsAny<ICriteria>(), symbol)).Returns(false);
            var coreSymbolService = new Mock<ICoreSymbolPackageService>(MockBehavior.Strict);
            var config = new Mock<IOptionsSnapshot<SymbolScanOnlyConfiguration>>();
            config.SetupGet(x => x.Value).Returns(new SymbolScanOnlyConfiguration());
            var validator = new SymbolScanValidator(Mock.Of<IValidationEntitiesContext>(), stateService.Object,
                coreSymbolService.Object, evaluator.Object, Mock.Of<IScanAndSignEnqueuer>(), config.Object,
                Mock.Of<ILogger<ScanAndSignProcessor>>(), entityService.Object, storage.Object);

            var result = await validator.StartAsync(new NuGetValidationRequest(Guid.NewGuid(), attempt.Key,
                "PackageA", "1.0.0", "https://example.test/symbols"));

            Assert.Equal(ValidationStatus.Succeeded, result.Status);
            Assert.Equal(ValidatingType.StagedSymbolPackage, state.ValidatingType);
            evaluator.Verify(x => x.IsMatch(It.IsAny<ICriteria>(), symbol), Times.Once);
        }

        private static StagedSymbolPackage CreateAttempt()
        {
            var attempt = new StagedSymbolPackage
            {
                Key = 43,
                StagedPackageIdentityKey = 42,
                UploadedBlobPath = "symbols/43",
                UploadedBlobETag = "etag",
                Status = StagedPackageStatus.Validating,
                StagedPackageIdentity = new StagedPackageIdentity
                {
                    Key = 42,
                    Package = new Package
                    {
                        PackageStatusKey = PackageStatus.Available,
                        NormalizedVersion = "1.0.0",
                        PackageRegistration = new PackageRegistration { Id = "PackageA" },
                    },
                },
            };
            attempt.StagedPackageIdentity.CurrentStagedSymbolPackageKey = attempt.Key;
            return attempt;
        }

        private class TestProvider : StagedSymbolPackageValidationSetProvider
        {
            public TestProvider(IValidationFileService fileService, IStagingBlobService blobService,
                IValidationStorageService storageService = null, ValidationConfiguration configuration = null)
                : base(storageService ?? Mock.Of<IValidationStorageService>(), fileService, blobService, Mock.Of<IValidatorProvider>(),
                    Options(configuration ?? new ValidationConfiguration
                    {
                        Validations = new List<ValidationConfigurationItem>
                        {
                            new ValidationConfigurationItem { Name = ValidatorName.SymbolScan, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                            new ValidationConfigurationItem { Name = ValidatorName.SymbolsValidator, ShouldStart = true, FailureBehavior = ValidationFailureBehavior.MustSucceed },
                            new ValidationConfigurationItem { Name = ValidatorName.SymbolsIngester, ShouldStart = true },
                        },
                    }),
                    Mock.Of<IOptionsSnapshot<SasDefinitionConfiguration>>(), Mock.Of<ITelemetryService>(),
                    Mock.Of<ILogger<ValidationSetProvider<StagedSymbolPackage>>>())
            {
            }

            public string[] GetValidationNames(StagedSymbolPackage attempt)
            {
                return GetValidationsToStart().Select(v => v.Name).ToArray();
            }

            private static IOptionsSnapshot<T> Options<T>(T value) where T : class
            {
                var options = new Mock<IOptionsSnapshot<T>>();
                options.SetupGet(x => x.Value).Returns(value);
                return options.Object;
            }
        }
    }
}
