// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Web.Configuration;

namespace NuGetGallery
{
    public class EntitiesContext : DbContext, IEntitiesContext
    {
        static EntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<EntitiesContext>(null);
        }

        /// <summary>
        /// The NuGet Gallery code should usually use this constructor, in order to respect read only mode.
        /// </summary>
        public EntitiesContext(string connectionString, bool readOnly)
            : base(connectionString)
        {
            ReadOnly = readOnly;
        }

        /// <summary>
        /// This constructor is provided mainly for purposes of running migrations from Package Manager console,
        /// or any other scenario where a connection string will be set after the EntitiesContext is created 
        /// (and read only mode is don't care).
        /// </summary>
        public EntitiesContext()
            : base("Gallery.SqlServer") // Use the connection string in a web.config (if one is found)
        {
        }

        public bool ReadOnly { get; private set; }
        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<Credential> Credentials { get; set; }
        public IDbSet<User> Users { get; set; }

        IDbSet<T> IEntitiesContext.Set<T>()
        {
            return base.Set<T>();
        }

        public override int SaveChanges()
        {
            if (ReadOnly)
            {
                throw new ReadOnlyModeException("Save changes unavailable: the gallery is currently in read only mode, with limited service. Please try again later.");
            }

            return base.SaveChanges();
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            Set<T>().Remove(entity);
        }

        public void SetCommandTimeout(int? seconds)
        {
            ((IObjectContextAdapter)this).ObjectContext.CommandTimeout = seconds;
        }

#pragma warning disable 618 // TODO: remove Package.Authors completely once prodution services definitely no longer need it
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Credential>()
                .HasKey(c => c.Key)
                .HasRequired(c => c.User)
                    .WithMany(u => u.Credentials)
                    .HasForeignKey(c => c.UserKey);

            modelBuilder.Entity<PackageLicenseReport>()
                .HasKey(r => r.Key)
                .HasMany(r => r.Licenses)
                .WithMany(l => l.Reports)
                .Map(c => c.ToTable("PackageLicenseReportLicenses")
                           .MapLeftKey("ReportKey")
                           .MapRightKey("LicenseKey"));

            modelBuilder.Entity<PackageLicense>()
                .HasKey(l => l.Key);

            modelBuilder.Entity<User>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<User>()
                .HasMany<EmailMessage>(u => u.Messages)
                .WithRequired(em => em.ToUser)
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<User>()
                .HasMany<Role>(u => u.Roles)
                .WithMany(r => r.Users)
                .Map(c => c.ToTable("UserRoles")
                           .MapLeftKey("UserKey")
                           .MapRightKey("RoleKey"));

            modelBuilder.Entity<Role>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasKey(em => em.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasOptional<User>(em => em.FromUser)
                .WithMany()
                .HasForeignKey(em => em.FromUserKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<Package>(pr => pr.Packages)
                .WithRequired(p => p.PackageRegistration)
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany()
                .Map(c => c.ToTable("PackageRegistrationOwners")
                           .MapLeftKey("PackageRegistrationKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<Package>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<Package>()
                .HasMany<PackageAuthor>(p => p.Authors)
                .WithRequired(pa => pa.Package)
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageStatistics>(p => p.DownloadStatistics)
                .WithRequired(ps => ps.Package)
                .HasForeignKey(ps => ps.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageDependency>(p => p.Dependencies)
                .WithRequired(pd => pd.Package)
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<PackageEdit>()
                .HasKey(pm => pm.Key);

            modelBuilder.Entity<PackageEdit>()
                .HasRequired(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageEdit>()
                .HasRequired<Package>(pm => pm.Package)
                .WithMany(p => p.PackageEdits)
                .HasForeignKey(pm => pm.PackageKey)
                .WillCascadeOnDelete(true); // Pending PackageEdits get deleted with their package, since hey, there's no way to apply them without the package anyway.

            modelBuilder.Entity<PackageHistory>()
                .HasKey(pm => pm.Key);

            modelBuilder.Entity<PackageHistory>()
                .HasOptional(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageHistory>()
                .HasRequired<Package>(pm => pm.Package)
                .WithMany(p => p.PackageHistories)
                .HasForeignKey(pm => pm.PackageKey)
                .WillCascadeOnDelete(true); // PackageHistories get deleted with their package.

            modelBuilder.Entity<PackageAuthor>()
                .HasKey(pa => pa.Key);

            modelBuilder.Entity<PackageStatistics>()
                .HasKey(ps => ps.Key);

            modelBuilder.Entity<PackageDependency>()
                .HasKey(pd => pd.Key);

            modelBuilder.Entity<GallerySetting>()
                .HasKey(gs => gs.Key);

            modelBuilder.Entity<PackageOwnerRequest>()
                .HasKey(por => por.Key);

            modelBuilder.Entity<PackageFramework>()
                .HasKey(pf => pf.Key);
            modelBuilder.Entity<CuratedFeed>()
                .HasKey(cf => cf.Key);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<CuratedPackage>(cf => cf.Packages)
                .WithRequired(cp => cp.CuratedFeed)
                .HasForeignKey(cp => cp.CuratedFeedKey);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<User>(cf => cf.Managers)
                .WithMany()
                .Map(c => c.ToTable("CuratedFeedManagers")
                           .MapLeftKey("CuratedFeedKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<CuratedPackage>()
                .HasKey(cp => cp.Key);

            modelBuilder.Entity<CuratedPackage>()
                .HasRequired(cp => cp.PackageRegistration);
        }
#pragma warning restore 618

    }
}
