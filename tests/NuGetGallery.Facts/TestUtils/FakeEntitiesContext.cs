// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using Xunit;

namespace NuGetGallery
{
    public class FakeEntitiesContext : IEntitiesContext
    {
        private Dictionary<Type, object> dbSets = new Dictionary<Type,object>();
        private bool areChangesSaved;

        public IDbSet<CuratedFeed> CuratedFeeds
        {
            get
            {
                return Set<CuratedFeed>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public IDbSet<CuratedPackage> CuratedPackages
        {
            get
            {
                return Set<CuratedPackage>();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

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

        public int SaveChanges()
        {
            areChangesSaved = true;
            return 0;
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
            Assert.True(areChangesSaved, "SaveChanges() has not been called on the entity context.");
        }


        public void SetCommandTimeout(int? seconds)
        {
            throw new NotSupportedException();
        }
    }
}

