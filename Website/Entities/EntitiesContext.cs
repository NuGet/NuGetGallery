using System.Configuration;
using System.Data.Common;
using System.Data.Entity;
using System.Data.SqlClient;
using MvcMiniProfiler.Data;

namespace NuGetGallery {
    public class EntitiesContext : DbContext {
        public EntitiesContext()
            : base(GetConnection("NuGetGallery"), contextOwnsConnection: true) {
        }

        public DbSet<Package> Packages { get; set; }
        public DbSet<PackageRegistration> PackageVersions { get; set; }
        public DbSet<EmailMessage> Messages { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder) {
            modelBuilder.Entity<User>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<User>()
                .HasMany<EmailMessage>(u => u.Messages)
                .WithOptional()
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<EmailMessage>()
                .HasKey(em => em.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasRequired<User>(em => em.ToUser)
                .WithMany()
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<EmailMessage>()
                .HasOptional<User>(em => em.FromUser)
                .WithMany()
                .HasForeignKey(em => em.FromUserKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<Package>(pr => pr.Packages)
                .WithOptional()
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany()
                .Map(c => c
                    .ToTable("PackageRegistrationOwners")
                    .MapLeftKey("PackageRegistrationKey")
                    .MapRightKey("UserKey"));

            modelBuilder.Entity<Package>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<Package>()
                .HasRequired<PackageRegistration>(p => p.PackageRegistration)
                .WithMany()
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageAuthor>(p => p.Authors)
                .WithOptional()
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageDependency>(p => p.Dependencies)
                .WithOptional()
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageReview>(p => p.Reviews)
                .WithOptional()
                .HasForeignKey(pr => pr.PackageKey);

            modelBuilder.Entity<PackageAuthor>()
                .HasKey(pa => pa.Key);

            modelBuilder.Entity<PackageAuthor>()
                .HasRequired<Package>(pa => pa.Package)
                .WithMany()
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<PackageDependency>()
                .HasKey(pd => pd.Key);

            modelBuilder.Entity<PackageDependency>()
                .HasRequired<Package>(pd => pd.Package)
                .WithMany()
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<PackageReview>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageReview>()
                .HasRequired<Package>(pr => pr.Package)
                .WithMany()
                .HasForeignKey(pr => pr.PackageKey);
        }

        private static DbConnection GetConnection(string connectionStringName) {
            var setting = ConfigurationManager.ConnectionStrings[connectionStringName];
            var connection = new SqlConnection(setting.ConnectionString);
            return ProfiledDbConnection.Get(connection);
        }

    }
}