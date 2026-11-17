// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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
        [Theory]
        [InlineData(0, 0)]
        [InlineData(2, 0)]
        [InlineData(0, 2)]
        [InlineData(1, 1)]
        public async Task CombinesArtifactCountsForBothQuotaChecks(int packageCount, int symbolCount)
        {
            var owner = new User("owner") { Key = 1 };
            var packages = new List<StagedPackage>();
            var symbols = new List<StagedSymbolPackage>();
            for (var key = 1; key <= packageCount; key++)
            {
                AddPackage(packages, key, owner);
            }
            for (var key = 1; key <= symbolCount; key++)
            {
                AddSymbols(symbols, new StagedPackageIdentity { OwnerKey = owner.Key }, key);
            }
            var limit = Math.Max(1, packageCount + symbolCount);
            var service = CreateService(packages, symbols, new AppConfiguration { StagingQuotaLimit = limit });

            var usage = service.GetUsage(owner);

            Assert.Equal(owner.Username, usage.Owner);
            Assert.Equal(packageCount, usage.UsedPackages);
            Assert.Equal(symbolCount, usage.UsedSymbols);
            Assert.Equal(limit, usage.Limit);
            if (usage.UsedArtifacts == limit)
            {
                await Assert.ThrowsAsync<StagingQuotaExceededException>(() => service.EnsureCapacityAsync(owner));
            }
            else
            {
                await service.EnsureCapacityAsync(owner);
            }
        }

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LimitsEachOwnerToTwentyRetainedGroupsIncludingEmptyAndExpiredGroups(bool organization)
        {
            User owner;
            if (organization)
            {
                owner = new Organization("dotnetframework") { Key = 1 };
            }
            else
            {
                owner = new User("owner") { Key = 1 };
            }

            var groups = Enumerable.Range(1, 19)
                .Select(key => new StagingGroup { Key = key, OwnerKey = owner.Key, ExpirationDate = DateTime.UtcNow.AddDays(-1) })
                .ToList();
            groups.Add(new StagingGroup { Key = 100, OwnerKey = 2 });
            var configuration = new AppConfiguration { StagingQuotaOwnerOverrides = "{\"dotnetframework\":10000}" };
            var service = CreateService(new List<StagedPackage>(), new List<StagedSymbolPackage>(), configuration, groups);

            await service.EnsureGroupCapacityAsync(owner);

            groups.Add(new StagingGroup { Key = 20, OwnerKey = owner.Key, ActivePromotionId = Guid.NewGuid() });
            await Assert.ThrowsAsync<StagingGroupLimitExceededException>(() => service.EnsureGroupCapacityAsync(owner));
            groups.Add(new StagingGroup { Key = 21, OwnerKey = owner.Key });
            await Assert.ThrowsAsync<StagingGroupLimitExceededException>(() => service.EnsureGroupCapacityAsync(owner));

            groups.RemoveAll(group => group.Key == 20 || group.Key == 21);
            await service.EnsureGroupCapacityAsync(owner);
        }

        [Fact]
        public async Task UsesConfiguredGroupLimitIndependentlyOfArtifactLimit()
        {
            var owner = new User("owner") { Key = 1 };
            var groups = new List<StagingGroup> { new StagingGroup { Key = 1, OwnerKey = owner.Key } };
            var configuration = new AppConfiguration { StagingGroupLimit = 2, StagingQuotaLimit = 1 };
            var service = CreateService(new List<StagedPackage>(), new List<StagedSymbolPackage>(), configuration, groups);

            await service.EnsureGroupCapacityAsync(owner);
            groups.Add(new StagingGroup { Key = 2, OwnerKey = owner.Key });
            await Assert.ThrowsAsync<StagingGroupLimitExceededException>(() => service.EnsureGroupCapacityAsync(owner));
            await service.EnsureCapacityAsync(owner);
        }

        private static StagingQuotaService CreateService(List<StagedPackage> packages, List<StagedSymbolPackage> symbols, AppConfiguration configuration, List<StagingGroup> groups = null)
        {
            var packageSet = new Mock<DbSet<StagedPackage>>().SetupDbSet(packages);
            packageSet.Setup(set => set.AsNoTracking()).Returns(packageSet.Object);
            var symbolSet = new Mock<DbSet<StagedSymbolPackage>>().SetupDbSet(symbols);
            symbolSet.Setup(set => set.AsNoTracking()).Returns(symbolSet.Object);
            var entities = new Mock<IEntitiesContext>();
            entities.Setup(context => context.StagedPackages).Returns(packageSet.Object);
            entities.Setup(context => context.StagedSymbolPackages).Returns(symbolSet.Object);
            var groupSet = new Mock<DbSet<StagingGroup>>().SetupDbSet(groups ?? new List<StagingGroup>());
            groupSet.Setup(set => set.AsNoTracking()).Returns(groupSet.Object);
            entities.Setup(context => context.StagingGroups).Returns(groupSet.Object);
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
