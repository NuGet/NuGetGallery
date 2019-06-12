// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGetGallery;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch
{
    public class DatabaseOwnerFetcherFacts
    {
        public class GetOwnersOrEmptyAsync : Facts
        {
            public GetOwnersOrEmptyAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task ReturnsEmptyArrayWhenPackageRegistrationDoesNotExist()
            {
                var owners = await Target.GetOwnersOrEmptyAsync(Data.PackageId);

                Assert.Empty(owners);
            }

            [Fact]
            public async Task ReturnsEmptyArrayWhenPackageRegistrationHasNoOwners()
            {
                PackageRegistrations.Add(new PackageRegistration
                {
                    Id = Data.PackageId,
                    Owners = new List<User>(),
                });

                var owners = await Target.GetOwnersOrEmptyAsync(Data.PackageId);

                Assert.Empty(owners);
            }

            [Fact]
            public async Task ReturnsSortedOwners()
            {
                PackageRegistrations.Add(new PackageRegistration
                {
                    Id = Data.PackageId,
                    Owners = new List<User>
                    {
                        new User { Username = "nuget" },
                        new User { Username = "aspnet" },
                        new User { Username = "EntityFramework" },
                        new User { Username = "Microsoft" },
                    }
                });

                var owners = await Target.GetOwnersOrEmptyAsync(Data.PackageId);

                Assert.Equal(new[] { "aspnet", "EntityFramework", "Microsoft", "nuget"}, owners);
                EntitiesContextFactory.Verify(x => x.CreateAsync(true), Times.Once);
                EntitiesContextFactory.Verify(x => x.CreateAsync(It.IsAny<bool>()), Times.Once);
            }

            [Fact]
            public async Task DisposesEntitiesContext()
            {
                var entitiesContext = new DisposableEntitiesContext();
                EntitiesContextFactory.Setup(x => x.CreateAsync(It.IsAny<bool>())).ReturnsAsync(entitiesContext);

                var owners = await Target.GetOwnersOrEmptyAsync(Data.PackageId);

                Assert.True(entitiesContext.Disposed, "The entities context should have been disposed.");
            }
        }

        public abstract class Facts
        {
            public Facts(ITestOutputHelper output)
            {
                SqlConnectionFactory = new Mock<ISqlConnectionFactory<GalleryDbConfiguration>>();
                EntitiesContextFactory = new Mock<IEntitiesContextFactory>();
                EntitiesContext = new Mock<IEntitiesContext>();
                TelemetryService = new Mock<IAzureSearchTelemetryService>();
                Logger = output.GetLogger<DatabaseOwnerFetcher>();

                PackageRegistrations = DbSetMockFactory.Create<PackageRegistration>();

                EntitiesContextFactory
                    .Setup(x => x.CreateAsync(It.IsAny<bool>()))
                    .ReturnsAsync(() => EntitiesContext.Object);
                EntitiesContext
                    .Setup(x => x.PackageRegistrations)
                    .Returns(() => PackageRegistrations);

                Target = new DatabaseOwnerFetcher(
                    SqlConnectionFactory.Object,
                    EntitiesContextFactory.Object,
                    TelemetryService.Object,
                    Logger);
            }

            public Mock<ISqlConnectionFactory<GalleryDbConfiguration>> SqlConnectionFactory { get; }
            public Mock<IEntitiesContextFactory> EntitiesContextFactory { get; }
            public Mock<IEntitiesContext> EntitiesContext { get; }
            public Mock<IAzureSearchTelemetryService> TelemetryService { get; }
            public RecordingLogger<DatabaseOwnerFetcher> Logger { get; }
            public DbSet<PackageRegistration> PackageRegistrations { get; }
            public DatabaseOwnerFetcher Target { get; }
        }

        private class DisposableEntitiesContext : IEntitiesContext, IDisposable
        {
            public DbSet<Certificate> Certificates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<PackageRegistration> PackageRegistrations { get; set; } = DbSetMockFactory.Create<PackageRegistration>();
            public DbSet<Credential> Credentials { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<Scope> Scopes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<User> Users { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<UserSecurityPolicy> UserSecurityPolicies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<ReservedNamespace> ReservedNamespaces { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<UserCertificate> UserCertificates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<SymbolPackage> SymbolPackages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public DbSet<Package> Packages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public bool Disposed { get; private set; }

            public void DeleteOnCommit<T>(T entity) where T : class
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                Disposed = true;
            }

            public IDatabase GetDatabase()
            {
                throw new NotImplementedException();
            }

            public Task<int> SaveChangesAsync()
            {
                throw new NotImplementedException();
            }

            public DbSet<T> Set<T>() where T : class
            {
                throw new NotImplementedException();
            }

            public void SetCommandTimeout(int? seconds)
            {
                throw new NotImplementedException();
            }
        }
    }
}
