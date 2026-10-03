// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class StagingQuotaServiceFacts
    {
        [Fact]
        public async Task CountsOnlyCurrentPrivateArtifactsIncludingExpiredStaging()
        {
            var owner = new User("owner") { Key = 1 };
            var packages = new List<StagedPackage>();
            var symbols = new List<StagedSymbolPackage>();
            var expired = AddPackage(packages, 1, owner);
            expired.ExpirationDate = DateTime.UtcNow.AddDays(-1);
            expired.StagedPackageIdentity.StagingGroupKey = 5;
            AddSymbols(symbols, expired.StagedPackageIdentity, 1);
            var publicParent = AddPackage(packages, 2, owner);
            publicParent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
            publicParent.Status = StagedPackageStatus.Succeeded;
            AddSymbols(symbols, publicParent.StagedPackageIdentity, 2).Status = StagedPackageStatus.FailedValidation;
            var deleted = AddPackage(packages, 3, owner);
            deleted.Status = StagedPackageStatus.Deleted;
            deleted.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
            AddSymbols(symbols, deleted.StagedPackageIdentity, 3).Status = StagedPackageStatus.WaitingForParent;
            var publishedParent = AddPackage(packages, 6, owner);
            publishedParent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
            publishedParent.Status = StagedPackageStatus.Succeeded;
            var publishedSymbols = AddSymbols(symbols, publishedParent.StagedPackageIdentity, 4);
            publishedSymbols.SymbolPackage.StatusKey = PackageStatus.Available;
            var retired = AddPackage(packages, 4, owner);
            retired.StagedPackageIdentity.CurrentStagedPackageKey = 99;
            AddPackage(packages, 5, new User("other") { Key = 2 });
            var service = CreateService(packages, symbols, new AppConfiguration { StagingQuotaLimit = 4 });

            var usage = service.GetUsage(owner);

            Assert.Equal(1, usage.UsedPackages);
            Assert.Equal(3, usage.UsedSymbols);
            Assert.Equal(4, usage.UsedArtifacts);
            Assert.Equal(4, usage.Limit);
            await Assert.ThrowsAsync<StagingQuotaExceededException>(() => service.EnsureCapacityAsync(owner));

            expired.Status = StagedPackageStatus.Deleted;
            expired.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Deleted;
            await service.EnsureCapacityAsync(owner);
            Assert.Equal(3, service.GetUsage(owner).UsedArtifacts);
        }

        [Fact]
        public async Task AppliesConfiguredOrganizationOverrideWithoutChangingOtherOwnersLimits()
        {
            var organization = new Organization("DotNet") { Key = 1 };
            var personal = new User("member") { Key = 2 };
            var packages = new List<StagedPackage>();
            AddPackage(packages, 1, organization);
            AddPackage(packages, 2, personal);
            var configuration = new AppConfiguration { StagingQuotaLimit = 1, StagingQuotaOwnerOverrides = "{\"dotnet\":5000}" };
            var service = CreateService(packages, new List<StagedSymbolPackage>(), configuration);

            Assert.Equal(5000, service.GetUsage(organization).Limit);
            Assert.Equal(1, service.GetUsage(personal).Limit);
            await service.EnsureCapacityAsync(organization);
            await Assert.ThrowsAsync<StagingQuotaExceededException>(() => service.EnsureCapacityAsync(personal));
        }

        [Fact]
        public void RejectsCaseInsensitiveDuplicateOwnerOverrides()
        {
            var configuration = new AppConfiguration { StagingQuotaOwnerOverrides = "{\"dotnet\":350,\"DOTNET\":5000}" };

            Assert.Throws<InvalidOperationException>(() => CreateService(new List<StagedPackage>(), new List<StagedSymbolPackage>(), configuration));
        }

        private static StagingQuotaService CreateService(List<StagedPackage> packages, List<StagedSymbolPackage> symbols, AppConfiguration configuration)
        {
            var packageSet = new Mock<DbSet<StagedPackage>>().SetupDbSet(packages);
            packageSet.Setup(set => set.AsNoTracking()).Returns(packageSet.Object);
            var symbolSet = new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(symbols);
            symbolSet.Setup(set => set.AsNoTracking()).Returns(symbolSet.Object);
            var entities = new Mock<IEntitiesContext>();
            entities.Setup(context => context.StagedPackages).Returns(packageSet.Object);
            entities.Setup(context => context.StagedSymbolPackages).Returns(symbolSet.Object);
            return new StagingQuotaService(entities.Object, configuration);
        }

        private static StagedPackage AddPackage(List<StagedPackage> packages, int key, User owner)
        {
            var identity = new StagedPackageIdentity
            {
                OwnerKey = owner.Key,
                Package = new Package { PackageStatusKey = PackageStatus.Staged },
                CurrentStagedPackageKey = key,
            };
            var attempt = new StagedPackage { Key = key, StagedPackageIdentity = identity, Status = StagedPackageStatus.Ready };
            packages.Add(attempt);
            return attempt;
        }

        private static StagedSymbolPackage AddSymbols(List<StagedSymbolPackage> symbols, StagedPackageIdentity identity, int key)
        {
            var attempt = new StagedSymbolPackage
            {
                Key = key,
                StagedPackageIdentity = identity,
                SymbolPackage = new SymbolPackage { StatusKey = PackageStatus.Staged },
                Status = StagedPackageStatus.Ready,
            };
            identity.CurrentStagedSymbolPackageKey = key;
            symbols.Add(attempt);
            return attempt;
        }
    }
}
