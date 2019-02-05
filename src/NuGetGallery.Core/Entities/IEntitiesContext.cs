// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IEntitiesContext
    {
        IDbSet<Certificate> Certificates { get; set; }
        IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        IDbSet<Credential> Credentials { get; set; }
        IDbSet<Scope> Scopes { get; set; }
        IDbSet<User> Users { get; set; }
        IDbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        IDbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        IDbSet<UserCertificate> UserCertificates { get; set; }
        IDbSet<SymbolPackage> SymbolPackages { get; set; }
        IDbSet<CVE> CVEs { get; set; }
        IDbSet<CWE> CWEs { get; set; }

        Task<int> SaveChangesAsync();
        IDbSet<T> Set<T>() where T : class;
        void DeleteOnCommit<T>(T entity) where T : class;
        void SetCommandTimeout(int? seconds);
        IDatabase GetDatabase();
    }
}