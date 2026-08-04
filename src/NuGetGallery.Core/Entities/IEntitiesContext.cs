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
        DbSet<PackageDependency> PackageDependencies { get; set; }
        DbSet<PackageFramework> PackageFrameworks { get; set; }
        DbSet<Credential> Credentials { get; set; }
        DbSet<Scope> Scopes { get; set; }
        DbSet<User> Users { get; set; }
        DbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        DbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        DbSet<UserCertificate> UserCertificates { get; set; }
        DbSet<SymbolPackage> SymbolPackages { get; set; }
        DbSet<PackageVulnerability> Vulnerabilities { get; set; }
        DbSet<VulnerablePackageVersionRange> VulnerableRanges { get; set; }
        DbSet<PackageRename> PackageRenames { get; set; }
        DbSet<StagingGroup> StagingGroups { get; set; }
        DbSet<StagingEntry> StagingEntries { get; set; }
        DbSet<StagedPackageArtifact> StagedPackageArtifacts { get; set; }
        DbSet<StagedSymbolArtifact> StagedSymbolArtifacts { get; set; }
        DbSet<StagingPromotionHistory> StagingPromotionHistories { get; set; }
        DbSet<StagingPromotionArtifactHistory> StagingPromotionArtifactHistories { get; set; }
        DbSet<StagingBlobCleanup> StagingBlobCleanups { get; set; }

        bool HasChanges { get; }

        Task<int> SaveChangesAsync();
        void DeleteOnCommit<T>(T entity) where T : class;
        IDatabase GetDatabase();
    }
}