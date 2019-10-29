// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IEntitiesContext : IReadOnlyEntitiesContext
    {
        DbSet<Certificate> Certificates { get; set; }
        DbSet<PackageDeprecation> Deprecations { get; set; }
        DbSet<PackageRegistration> PackageRegistrations { get; set; }
        DbSet<Credential> Credentials { get; set; }
        DbSet<Scope> Scopes { get; set; }
        DbSet<User> Users { get; set; }
        DbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        DbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        DbSet<UserCertificate> UserCertificates { get; set; }
        DbSet<SymbolPackage> SymbolPackages { get; set; }
        DbSet<PackageVulnerability> Vulnerabilities { get; set; }
        DbSet<VulnerablePackageVersionRange> VulnerableRanges { get; set; }

        Task<int> SaveChangesAsync();
        void DeleteOnCommit<T>(T entity) where T : class;
        IDatabase GetDatabase();
    }
}