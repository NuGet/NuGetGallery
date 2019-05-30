// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReadOnlyEntitiesContext : IReadOnlyEntitiesContext
    {
        private readonly EntitiesContext _entitiesContext; 
        
        public ReadOnlyEntitiesContext(DbConnection connection)
        { 
            _entitiesContext = new EntitiesContext(connection, readOnly: true);
        }

        public DbSet<Package> Packages
        {
            get
            {
                return _entitiesContext.Packages;
            }
            set
            {
                _entitiesContext.Packages = value;
            }
        }

        public DbSet<PackageRegistration> PackageRegistrations
        {
            get
            {
                return _entitiesContext.PackageRegistrations;
            }
            set
            {
                _entitiesContext.PackageRegistrations = value;
            }
        }

        public DbSet<Credential> Credentials
        {
            get
            {
                return _entitiesContext.Credentials;
            }
            set
            {
                _entitiesContext.Credentials = value;
            }
        }

        public DbSet<Scope> Scopes
        {
            get
            {
                return _entitiesContext.Scopes;
            }
            set
            {
                _entitiesContext.Scopes = value;
            }
        }

        public DbSet<UserSecurityPolicy> UserSecurityPolicies
        {
            get
            {
                return _entitiesContext.UserSecurityPolicies;
            }
            set
            {
                _entitiesContext.UserSecurityPolicies = value;
            }
        }

        public DbSet<ReservedNamespace> ReservedNamespaces
        {
            get
            {
                return _entitiesContext.ReservedNamespaces;
            }
            set
            {
                _entitiesContext.ReservedNamespaces = value;
            }
        }

        public DbSet<Certificate> Certificates
        {
            get
            {
                return _entitiesContext.Certificates;
            }
            set
            {
                _entitiesContext.Certificates = value;
            }
        }

        public DbSet<UserCertificate> UserCertificates
        {
            get
            {
                return _entitiesContext.UserCertificates;
            }
            set
            {
                _entitiesContext.UserCertificates = value;
            }
        }

        public DbSet<SymbolPackage> SymbolPackages
        {
            get
            {
                return _entitiesContext.SymbolPackages;
            }
            set
            {
                _entitiesContext.SymbolPackages = value;
            }
        }

        /// <summary>
        /// User or organization accounts.
        /// </summary>
        public DbSet<User> Users
        {
            get
            {
                return _entitiesContext.Users;
            }
            set
            {
                _entitiesContext.Users = value;
            }
        }

        DbSet<T> IReadOnlyEntitiesContext.Set<T>()
        {
            return _entitiesContext.Set<T>();
        }

        public void SetCommandTimeout(int? seconds)
        {
            _entitiesContext.SetCommandTimeout(seconds);
        }

        public void Dispose()
        {
            _entitiesContext.Dispose(); ;
        }
    }
}