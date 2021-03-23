// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class FakeEntitiesContext : IEntitiesContext
    {
        private readonly Dictionary<Type, object> dbSets = new Dictionary<Type, object>();
        private bool _areChangesSaved;

        public DbSet<PackageRegistration> PackageRegistrations
        {
            get => Set<PackageRegistration>();
            set => throw new NotSupportedException();
        }

        public DbSet<PackageDependency> PackageDependencies
        {
            get => Set<PackageDependency>();
            set => throw new NotSupportedException();
        }

        public DbSet<PackageFramework> PackageFrameworks
        {
            get => Set<PackageFramework>();
            set => throw new NotSupportedException();
        }

        public DbSet<Package> Packages
        {
            get => Set<Package>();
            set => throw new NotSupportedException();
        }

        public DbSet<PackageDeprecation> Deprecations
        {
            get => Set<PackageDeprecation>();
            set => throw new NotSupportedException();
        }

        public DbSet<Credential> Credentials
        {
            get => Set<Credential>();
            set => throw new NotSupportedException();
        }

        public DbSet<Scope> Scopes
        {
            get => Set<Scope>();
            set => throw new NotSupportedException();
        }

        public DbSet<User> Users
        {
            get => Set<User>();
            set => throw new NotSupportedException();
        }

        public DbSet<Organization> Organizations
        {
            get => Set<Organization>();
            set => throw new NotSupportedException();
        }

        public DbSet<UserSecurityPolicy> UserSecurityPolicies
        {
            get => Set<UserSecurityPolicy>();
            set => throw new NotSupportedException();
        }

        public DbSet<ReservedNamespace> ReservedNamespaces
        {
            get => Set<ReservedNamespace>();
            set => throw new NotSupportedException();
        }

        public DbSet<Certificate> Certificates
        {
            get => Set<Certificate>();
            set => throw new NotSupportedException();
        }

        public DbSet<UserCertificate> UserCertificates
        {
            get => Set<UserCertificate>();
            set => throw new NotSupportedException();
        }

        public DbSet<SymbolPackage> SymbolPackages
        {
            get => Set<SymbolPackage>();
            set => throw new NotSupportedException();
        }

        public DbSet<PackageVulnerability> Vulnerabilities
        {
            get => Set<PackageVulnerability>();
            set => throw new NotSupportedException();
        }

        public DbSet<VulnerablePackageVersionRange> VulnerableRanges
        {
            get => Set<VulnerablePackageVersionRange>();
            set => throw new NotSupportedException();
        }

        public DbSet<PackageRename> PackageRenames
        {
            get => Set<PackageRename>();
            set => throw new NotSupportedException();
        }

        public virtual string QueryHint => throw new NotImplementedException();

        public Task<int> SaveChangesAsync()
        {
            _areChangesSaved = true;
            return Task.FromResult(0);
        }

        public DbSet<T> Set<T>() where T : class
        {
            if (!dbSets.ContainsKey(typeof(T)))
            {
                dbSets.Add(typeof(T), CreateDbSet<T>());
            }

            return (DbSet<T>)dbSets[typeof(T)];
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            Set<T>().Remove(entity);
        }

        public void VerifyCommitChanges()
        {
            Assert.True(_areChangesSaved, "SaveChanges() has not been called on the entity context.");
        }


        public void SetCommandTimeout(int? seconds)
        {
            throw new NotSupportedException();
        }

        private IDatabase _database;
        public IDatabase GetDatabase()
        {
            return _database ?? throw new NotSupportedException();
        }

        public void SetupDatabase(IDatabase database)
        {
            _database = database;
        }

        public static DbSet<T> CreateDbSet<T>() where T : class
        {
            var data = new List<T>();
            var mockSet = new Mock<DbSet<T>>();
            mockSet
                .As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(() => data.AsQueryable().Provider);

            mockSet
                .As<IQueryable<T>>()
                .Setup(m => m.Expression)
                .Returns(() => data.AsQueryable().Expression);

            mockSet
                .As<IQueryable<T>>()
                .Setup(m => m.ElementType)
                .Returns(() => data.AsQueryable().ElementType);

            mockSet
                .As<IQueryable<T>>()
                .Setup(m => m.GetEnumerator())
                .Returns(() => data.GetEnumerator());

            mockSet
                .Setup(x => x.Include(It.IsAny<string>()))
                .Returns(mockSet.Object);

            mockSet
                .Setup(x => x.Add(It.IsAny<T>()))
                .Callback<T>(x => data.Add(x));

            mockSet
                .Setup(x => x.AddRange(It.IsAny<IEnumerable<T>>()))
                .Callback<IEnumerable<T>>(x => data.AddRange(x));

            mockSet
                .Setup(x => x.Remove(It.IsAny<T>()))
                .Callback<T>(x => data.Remove(x));

            mockSet
                .Setup(x => x.RemoveRange(It.IsAny<IEnumerable<T>>()))
                .Callback<IEnumerable<T>>(x =>
                {
                    foreach (var item in x)
                    {
                        data.Remove(item);
                    }
                });

            mockSet
                .Setup(x => x.Local)
                .Returns(() => new ObservableCollection<T>(data));

            return mockSet.Object;
        }

        public void Dispose()
        {
        }

        public virtual IDisposable WithQueryHint(string queryHint)
        {
            throw new NotImplementedException();
        }
    }
}
