// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class FakeEntitiesContext : IEntitiesContext
    {
        private readonly Dictionary<Type, object> dbSets = new Dictionary<Type,object>();
        private bool _areChangesSaved;

        public IDbSet<PackageRegistration> PackageRegistrations
        {
            get
            {
                return Set<PackageRegistration>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Package> Packages
        {
            get
            {
                return Set<Package>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Credential> Credentials
        {
            get
            {
                return Set<Credential>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Scope> Scopes
        {
            get
            {
                return Set<Scope>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<User> Users
        {
            get
            {
                return Set<User>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Organization> Organizations
        {
            get
            {
                return Set<Organization>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<UserSecurityPolicy> UserSecurityPolicies
        {
            get
            {
                return Set<UserSecurityPolicy>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<ReservedNamespace> ReservedNamespaces
        {
            get
            {
                return Set<ReservedNamespace>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Certificate> Certificates
        {
            get
            {
                return Set<Certificate>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<UserCertificate> UserCertificates
        {
            get
            {
                return Set<UserCertificate>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<SymbolPackage> SymbolPackages
        {
            get
            {
                return Set<SymbolPackage>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<Cve> Cves
        {
            get
            {
                return Set<Cve>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
        public IDbSet<Cwe> Cwes
        {
            get
            {
                return Set<Cwe>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public Task<int> SaveChangesAsync()
        {
            _areChangesSaved = true;
            return Task.FromResult(0);
        }

        public IDbSet<T> Set<T>() where T : class
        {
            if (!dbSets.ContainsKey(typeof(T)))
            {
                dbSets.Add(typeof(T), new FakeDbSet<T>(this));
            }

            return (IDbSet<T>)(dbSets[typeof(T)]);
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            ((FakeDbSet<T>)(Set<T>())).Remove(entity);
        }

        public void VerifyCommitChanges()
        {
            Assert.True(_areChangesSaved, "SaveChanges() has not been called on the entity context.");
        }


        public void SetCommandTimeout(int? seconds)
        {
            throw new NotSupportedException();
        }

        public IDatabase GetDatabase()
        {
            throw new NotSupportedException();
        }
    }
}

