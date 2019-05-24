// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class BulkPackageUpdateServiceFacts
    {
        public class TheUpdatePackagesAsyncMethod : TestContainer
        {
            private readonly Mock<IEntitiesContext> _mockEntitiesContext;
            private readonly Mock<IPackageService> _mockPackageService;
            private readonly Mock<IDatabase> _mockDatabase;

            public TheUpdatePackagesAsyncMethod()
            {
                _mockEntitiesContext = GetMock<IEntitiesContext>();
                _mockPackageService = GetMock<IPackageService>();

                _mockDatabase = GetMock<IDatabase>();
                _mockEntitiesContext
                    .Setup(x => x.GetDatabase())
                    .Returns(_mockDatabase.Object)
                    .Verifiable();
            }

            public static IEnumerable<object[]> SetListed_Data => MemberDataHelper.AsDataSet(null, false, true);

            public static IEnumerable<object[]> ThrowsIfNullOrEmptyPackages_Data => 
                MemberDataHelper.Combine(
                    MemberDataHelper.AsDataSet(null, new Package[0]),
                    SetListed_Data);

            [Theory]
            [MemberData(nameof(ThrowsIfNullOrEmptyPackages_Data))]
            public async Task ThrowsIfNullOrEmptyPackages(IEnumerable<Package> packages, bool? setListed)
            {
                var service = Get<BulkPackageUpdateService>();
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.UpdatePackagesAsync(packages, setListed));
            }

            private static IEnumerable<RegistrationTestBuilder> PossiblePackageCombinations
            {
                get
                {
                    var num = 0;
                    foreach (var latestState in 
                        Enum.GetValues(typeof(PackageLatestState)).Cast<PackageLatestState>())
                    {
                        foreach (var listed in new [] { false, true })
                        {
                            yield return new RegistrationTestBuilder("howdy" + num++, new PackageTestBuilder(num + ".0.0", listed, latestState));
                        }
                    }
                }
            }

            public static IEnumerable<object[]> PackageCombinationsAndSetListed_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.AsDataSet(PossiblePackageCombinations.SelectMany(pr => pr.Build()).ToList()),
                    SetListed_Data);

            [Theory]
            [MemberData(nameof(PackageCombinationsAndSetListed_Data))]
            public Task ThrowsWhenSqlQueryFails(IEnumerable<Package> packages, bool? setListed)
            {
                return Assert.ThrowsAsync<InvalidOperationException>(() => SetupAndInvokeMethod(packages, setListed, false));
            }

            [Theory]
            [MemberData(nameof(PackageCombinationsAndSetListed_Data))]
            public async Task SuccessfullyUpdatesPackages(IEnumerable<Package> packages, bool? setListed)
            {
                var expectedListed = packages.ToDictionary(p => p.Key, p => setListed ?? p.Listed);

                await SetupAndInvokeMethod(packages, setListed, true);

                foreach (var package in packages)
                {
                    Assert.Equal(expectedListed[package.Key], package.Listed);
                }

                _mockPackageService.Verify();
                _mockEntitiesContext.Verify();
                _mockDatabase.Verify();
            }

            private Task SetupAndInvokeMethod(IEnumerable<Package> packages, bool? setListed, bool sqlQuerySucceeds)
            {
                // Fallback UpdateIsLatestAsync setup
                // The latter setups will override this one if they apply
                _mockPackageService
                    .Setup(x => x.UpdateIsLatestAsync(It.IsAny<PackageRegistration>(), It.IsAny<bool>()))
                    .Throws(new Exception($"Unexpected {nameof(IPackageService.UpdateIsLatestAsync)} call!"));

                if (setListed.HasValue)
                {
                    foreach (var packagesByRegistration in packages.GroupBy(p => p.PackageRegistration))
                    {
                        if (!packagesByRegistration.Any(p => p.IsLatest || p.IsLatestStable || p.IsLatestSemVer2 || p.IsLatestStableSemVer2))
                        {
                            continue;
                        }

                        _mockPackageService
                            .Setup(x => x.UpdateIsLatestAsync(packagesByRegistration.Key, false))
                            .Returns(Task.CompletedTask)
                            .Verifiable();
                    }

                    _mockEntitiesContext
                        .Setup(x => x.SaveChangesAsync())
                        .Returns(Task.FromResult(0))
                        .Verifiable();
                }
                else
                {
                    _mockEntitiesContext
                        .Setup(x => x.SaveChangesAsync())
                        .Throws(new Exception($"Unexpected {nameof(IEntitiesContext.SaveChangesAsync)} call!"));
                }

                var packageKeyStrings = string.Join(", ", packages.Select(p => p.Key));

                var expectedQuery = $@"
UPDATE [dbo].Packages
SET LastEdited = GETUTCDATE(), LastUpdated = GETUTCDATE()
WHERE [Key] IN ({packageKeyStrings})";

                _mockDatabase
                    .Setup(x => x.ExecuteSqlCommandAsync(It.Is<string>(q => q != expectedQuery)))
                    .Throws(new Exception($"Unexpected {nameof(IDatabase.ExecuteSqlCommandAsync)} call!"));

                _mockDatabase
                    .Setup(x => x.ExecuteSqlCommandAsync(expectedQuery))
                    .Returns(Task.FromResult(sqlQuerySucceeds ? packages.Count() * 2 : 0))
                    .Verifiable();

                return Get<BulkPackageUpdateService>()
                    .UpdatePackagesAsync(packages, setListed);
            }

            private static int _key = 0;

            private class RegistrationTestBuilder
            {
                public RegistrationTestBuilder(
                    string id, 
                    IEnumerable<PackageTestBuilder> packages)
                {
                    Id = id;
                    Packages = packages;
                }
                public RegistrationTestBuilder(
                    string id,
                    params PackageTestBuilder[] packages)
                    : this(id, packages.ToList())
                {
                }

                public string Id { get; }
                public IEnumerable<PackageTestBuilder> Packages { get; }

                public IEnumerable<Package> Build()
                {
                    var registration = new PackageRegistration
                    {
                        Key = Interlocked.Increment(ref _key),
                        Id = Id
                    };

                    foreach (var package in Packages)
                    {
                        package.Build(registration);
                    }

                    return registration.Packages;
                }
            }

            private enum PackageLatestState
            {
                Not,
                Latest,
                LatestStable,
                LatestSemVer2,
                LatestStableSemVer2
            }

            private class PackageTestBuilder
            {
                public PackageTestBuilder(
                    string version,
                    bool listed = false,
                    PackageLatestState latest = PackageLatestState.Not)
                {
                    Version = version;
                    Listed = listed;
                    Latest = latest;
                }

                public string Version { get; }
                public bool Listed { get; }
                public PackageLatestState Latest { get; }

                public Package Build(PackageRegistration registration)
                {
                    var package = new Package
                    {
                        Key = Interlocked.Increment(ref _key),
                        PackageRegistration = registration,
                        Version = Version,
                        Listed = Listed
                    };

                    switch (Latest)
                    {
                        case PackageLatestState.Not:
                            break;
                        case PackageLatestState.Latest:
                            package.IsLatest = true;
                            break;
                        case PackageLatestState.LatestStable:
                            package.IsLatestStable = true;
                            break;
                        case PackageLatestState.LatestSemVer2:
                            package.IsLatestSemVer2 = true;
                            break;
                        case PackageLatestState.LatestStableSemVer2:
                            package.IsLatestStableSemVer2 = true;
                            break;
                        default:
                            throw new ArgumentException(nameof(Latest));
                    }

                    registration.Packages.Add(package);
                    return package;
                }
            }
        }
    }
}
