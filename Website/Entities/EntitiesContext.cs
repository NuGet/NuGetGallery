using System.Data.Entity;
using System.Linq;
using WebBackgrounder;

namespace NuGetGallery
{
    public interface IEntitiesContext
    {
        int SaveChanges();
        DbSet<T> Set<T>() where T : class;

        IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        IDbSet<CuratedPackage> CuratedPackages { get; set; }
        IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        IDbSet<User> Users { get; set; }        
    }

    public class EntitiesContext : DbContext, IWorkItemsContext, IEntitiesContext
    {
        public EntitiesContext()
            : base("NuGetGallery")
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
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
                .Map(c => c
                    .ToTable("UserRoles")
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
                .Map(c => c
                    .ToTable("PackageRegistrationOwners")
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

            modelBuilder.Entity<WorkItem>()
                .HasKey(wi => wi.Id);

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
                .Map(c => c
                    .ToTable("CuratedFeedManagers")
                    .MapLeftKey("CuratedFeedKey")
                    .MapRightKey("UserKey"));

            modelBuilder.Entity<CuratedPackage>()
                .HasKey(cp => cp.Key);

            modelBuilder.Entity<CuratedPackage>()
                .HasRequired(cp => cp.PackageRegistration);
        }

        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<User> Users { get; set; } 
        public IDbSet<WorkItem> WorkItems
        {
            get;
            set;
        }
    }
}