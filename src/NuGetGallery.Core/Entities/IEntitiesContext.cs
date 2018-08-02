﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IEntitiesContext
    {
        IDbSet<Certificate> Certificates { get; set; }
        IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        IDbSet<CuratedPackage> CuratedPackages { get; set; }
        IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        IDbSet<Credential> Credentials { get; set; }
        IDbSet<Scope> Scopes { get; set; }
        IDbSet<User> Users { get; set; }
        IDbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        IDbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        IDbSet<UserCertificate> UserCertificates { get; set; }

        Task<int> SaveChangesAsync();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Set", Justification="This is to match the EF terminology.")]
        IDbSet<T> Set<T>() where T : class;
        void DeleteOnCommit<T>(T entity) where T : class;
        void SetCommandTimeout(int? seconds);
        IDatabase GetDatabase();
    }
}