using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Ninject;
using NuGetGallery.Data.Model;
using System.Collections.Generic;

namespace NuGetGallery.Data
{
    public class EntitiesContext : DbContext, IEntitiesContext
    {
        [Obsolete("Stop! If you're constructing a context manually, you're probably doing something wrong. Use Ninject or IEntityContextFactory.")]
        public EntitiesContext(string connectionString, DbCompiledModel model, bool readOnly)
            : base(connectionString, model)
        {
            ReadOnly = readOnly;
        }

        public bool ReadOnly { get; private set; }
        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<User> Users { get; set; }

        public override int SaveChanges()
        {
            if (ReadOnly)
            {
                throw new ReadOnlyModeException("Save changes unavailable: the gallery is currently in read only mode, with limited service. Please try again later.");
            }

            return base.SaveChanges();
        }

        // NOTE: OnModelCreating has moved to DbModelFactory.cs
    }
}
