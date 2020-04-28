using NuGet.Services.Entities;
using NuGetGallery;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VerifyMicrosoftPackage.Fakes
{
    class FakeEntitiesContext : IEntitiesContext
    {
        public DbSet<Certificate> Certificates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<PackageDeprecation> Deprecations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<PackageRegistration> PackageRegistrations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<Credential> Credentials { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<Scope> Scopes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<User> Users { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<UserSecurityPolicy> UserSecurityPolicies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<ReservedNamespace> ReservedNamespaces { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<UserCertificate> UserCertificates { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<SymbolPackage> SymbolPackages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<PackageVulnerability> Vulnerabilities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<VulnerablePackageVersionRange> VulnerableRanges { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public DbSet<Package> Packages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
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
