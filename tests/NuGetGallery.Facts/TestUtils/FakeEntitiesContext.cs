using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;

namespace NuGetGallery
{
    public class FakeEntitiesContext : IEntitiesContext
    {
        Dictionary<Type, object> dbSets = new Dictionary<Type,object>();

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
    }
}
