// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IEntitiesContext : IDisposable
    {
        DbSet<Certificate> Certificates { get; set; }
        DbSet<Package> Packages { get; set; }
        DbSet<PackageRegistration> PackageRegistrations { get; set; }
        DbSet<Credential> Credentials { get; set; }
        DbSet<Scope> Scopes { get; set; }
        DbSet<User> Users { get; set; }
        DbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        DbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        DbSet<UserCertificate> UserCertificates { get; set; }
        DbSet<SymbolPackage> SymbolPackages { get; set; }

        Task<int> SaveChangesAsync();
        DbSet<T> Set<T>() where T : class;
        void DeleteOnCommit<T>(T entity) where T : class;
        void SetCommandTimeout(int? seconds);
        IDatabase GetDatabase();
    }
}