using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NuGetGallery.Data.Model;

namespace NuGetGallery.Data
{
    public class DbModelFactory : IDbModelFactory
    {
        private static readonly MethodInfo EntityMethod = typeof(DbModelBuilder).GetMethod("Entity",
                                                                                            BindingFlags.Public |
                                                                                            BindingFlags.Instance);
        private static readonly MethodInfo ConfigureEntityPropertyMethod = typeof(DbModelFactory).GetMethod("ConfigureEntityProperty",
                                                                                            BindingFlags.NonPublic |
                                                                                            BindingFlags.Instance);

        public IDatabaseVersioningService VersioningService { get; protected set; }
        public Assembly ModelsAssembly { get; protected set; }
        public string ModelsNamespace { get; protected set; }

        protected DbModelFactory()
        {
        }

        public DbModelFactory(IDatabaseVersioningService versioningService, Assembly modelsAssembly, string modelsNamespace) : this()
        {
            VersioningService = versioningService;
            ModelsAssembly = modelsAssembly;
            ModelsNamespace = modelsNamespace;
        }

        public DbCompiledModel CreateModel()
        {
            // Ensure we're at the minimum allowed migration version
            if (VersioningService != null)
            {
                VersioningService.UpdateToMinimum();
            }

            var modelBuilder = new DbModelBuilder(DbModelBuilderVersion.Latest);

            // Load the entities in to the model
            var entities = from t in ModelsAssembly.GetExportedTypes()
                           where t.IsClass && !t.IsAbstract && String.Equals(t.Namespace, ModelsNamespace, StringComparison.Ordinal)
                           select t;
            foreach (var entityType in entities)
            {
                object config = EntityMethod.MakeGenericMethod(entityType).Invoke(modelBuilder, new object[0]);

                ConfigureEntity(config, entityType);
            }

            ConfigureModel(modelBuilder);

            // Build, compile and return the model
            return modelBuilder.Build(new DbProviderInfo("System.Data.SqlClient", "2008")).Compile();
        }

        private void ConfigureEntity(dynamic config, Type entityType)
        {
            var ignoredProperties =
                from p in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                let a = p.GetCustomAttribute<RequiresMigrationAttribute>()
                // Null Versioning Service ==> Model everything
                where a != null && (VersioningService == null || !VersioningService.HasVersion(a.MigrationId))
                select p;
            foreach (var property in ignoredProperties)
            {
                // Configure this property
                ConfigureEntityPropertyMethod.MakeGenericMethod(entityType, property.PropertyType)
                    .Invoke(this, new object[] { config, property });
            }
        }

        // Called via reflection. We need to call Expression.Lambda<Func<?, ?>> and the easiest way to do that
        // is via our own generic method
        private void ConfigureEntityProperty<TEntity, TProperty>(dynamic config, PropertyInfo property)
        {
            var param = Expression.Parameter(typeof(TEntity));
            var expr =
                Expression.Lambda<Func<TEntity, TProperty>>(
                    Expression.Property(param, property),
                    param);
            config.Ignore(expr);
        }

        // Configures the model for the base Migration (the last one before we moved to a more flexible model)
        public static void ConfigureModel(DbModelBuilder modelBuilder)
        {
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
    }
}