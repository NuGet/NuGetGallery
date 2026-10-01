// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    /// <summary>
    /// Verifies promotion storage isolation using the orchestrator's host registrations.
    /// </summary>
    public class JobFacts
    {
        [Fact]
        public void PublicPackageStorageUsesItsOwnAccountWithoutChangingValidationStorage()
        {
            var builder = CreateBuilder();
            using (var container = builder.Build())
            {
                var publicClient = container.ResolveKeyed<ICloudBlobClient>("PackageStorage");
                var validationClient = container.Resolve<ICloudBlobClient>();

                Assert.Equal("package-storage.test", publicClient.GetContainerReference("symbols").GetBlobReference("packagea.1.0.0.snupkg").Uri.Host);
                Assert.Equal("validation-storage.test", validationClient.GetContainerReference("symbols").GetBlobReference("packagea.1.0.0.snupkg").Uri.Host);
            }
        }

        [Fact]
        public async Task SymbolPromotionPublishesThroughPublicStorageRatherThanValidationStorage()
        {
            var promotionId = Guid.NewGuid();
            var parent = new Package
            {
                Key = 42,
                NormalizedVersion = "1.0.0",
                PackageStatusKey = PackageStatus.Available,
                PackageRegistration = new PackageRegistration { Id = "PackageA", Owners = new List<User> { new User { Key = 7 } } },
            };
            var symbol = new SymbolPackage { Key = 44, PackageKey = parent.Key, StatusKey = PackageStatus.Staged };
            var attempt = new StagedSymbolPackage
            {
                Key = 43,
                Status = StagedPackageStatus.Promoting,
                ActivePromotionId = promotionId,
                UploadedBlobPath = "symbols/43",
                UploadedBlobETag = "etag",
                SymbolPackage = symbol,
                StagedPackageIdentity = new StagedPackageIdentity { Package = parent, OwnerKey = 7, CurrentStagedSymbolPackageKey = 43 },
            };
            var attempts = new Mock<IEntityRepository<StagedSymbolPackage>>();
            attempts.Setup(repository => repository.GetAll()).Returns(new[] { attempt }.AsQueryable());
            attempts.Setup(repository => repository.ExecuteInTransactionAsync(It.IsAny<Func<Task>>())).Returns((Func<Task> action) => action());
            attempts.Setup(repository => repository.CommitChangesAsync()).Returns(Task.CompletedTask);
            var symbols = new Mock<IEntityRepository<SymbolPackage>>();
            symbols.Setup(repository => repository.GetAll()).Returns(new[] { symbol }.AsQueryable());
            var symbolService = new Mock<ICoreSymbolPackageService>();
            symbolService.Setup(service => service.UpdateStatusAsync(symbol, PackageStatus.Available, false))
                .Callback(() => symbol.StatusKey = PackageStatus.Available).Returns(Task.CompletedTask);
            var uri = new Uri("https://staging-storage.test/symbols/43");
            var stagingBlobs = new Mock<IStagingBlobService>();
            stagingBlobs.Setup(service => service.GetPackageReadUriAsync("symbols/43", "etag")).ReturnsAsync(uri);
            var publicStorage = new Mock<ICoreFileStorageService>();
            publicStorage.Setup(service => service.CopyFileAsync(uri, CoreConstants.Folders.SymbolPackagesFolderName,
                "packagea.1.0.0.snupkg", It.IsAny<IAccessCondition>())).Returns(Task.CompletedTask);
            var validationStorage = new Mock<ICoreFileStorageService>(MockBehavior.Strict);
            var builder = CreateBuilder();
            builder.RegisterInstance(attempts.Object).As<IEntityRepository<StagedSymbolPackage>>();
            builder.RegisterInstance(symbols.Object).As<IEntityRepository<SymbolPackage>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagedPackageIdentity>>()).As<IEntityRepository<StagedPackageIdentity>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagedPackage>>()).As<IEntityRepository<StagedPackage>>();
            builder.RegisterInstance(Mock.Of<IEntityRepository<StagingGroup>>()).As<IEntityRepository<StagingGroup>>();
            builder.RegisterInstance(Mock.Of<ILogger<StagingGroupPromotionService>>()).As<ILogger<StagingGroupPromotionService>>();
            builder.RegisterInstance(symbolService.Object).As<ICoreSymbolPackageService>();
            builder.RegisterInstance(stagingBlobs.Object).As<IStagingBlobService>();
            builder.RegisterInstance(publicStorage.Object).Keyed<ICoreFileStorageService>("PackageStorage");
            builder.RegisterInstance(validationStorage.Object).As<ICoreFileStorageService>();
            builder.RegisterInstance(Mock.Of<ILogger<StagedSymbolPackagePromotionService>>()).As<ILogger<StagedSymbolPackagePromotionService>>();
            using (var container = builder.Build())
            {
                await container.Resolve<IStagedSymbolPackagePromotionService>().CompleteAsync(attempt.Key, promotionId);

                publicStorage.Verify(service => service.CopyFileAsync(uri, CoreConstants.Folders.SymbolPackagesFolderName,
                    "packagea.1.0.0.snupkg", It.Is<IAccessCondition>(condition => condition.IfNoneMatchETag == "*")), Times.Once);
                validationStorage.VerifyNoOtherCalls();
                Assert.Equal(PackageStatus.Available, symbol.StatusKey);
                Assert.Equal(StagedPackageStatus.Succeeded, attempt.Status);
            }
        }

        private static ContainerBuilder CreateBuilder()
        {
            var builder = new ContainerBuilder();
            new JobHarness().Configure(builder);
            var configuration = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            configuration.SetupGet(options => options.Value).Returns(new ValidationConfiguration
            {
                PackageStorageConnectionString = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://package-storage.test",
                ValidationStorageConnectionString = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://validation-storage.test",
            });
            builder.RegisterInstance(configuration.Object).As<IOptionsSnapshot<ValidationConfiguration>>();
            builder.RegisterInstance(Options.Create(new StorageMsiConfiguration())).As<IOptions<StorageMsiConfiguration>>();
            return builder;
        }

        /// <summary>
        /// Exposes host bindings without starting subscriptions or connecting to external services.
        /// </summary>
        private class JobHarness : Job
        {
            public void Configure(ContainerBuilder builder)
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "RunnerConfiguration:ValidatingType", "SymbolPackage" },
                }).Build();
                ConfigureAutofacServices(builder, configuration);
            }
        }
    }
}
