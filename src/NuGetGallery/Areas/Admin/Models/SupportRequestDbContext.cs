namespace NuGetGallery.Areas.Admin.Models
{
    using System.Data.Entity;

    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public partial class SupportRequestDbContext 
        : DbContext, ISupportRequestDbContext
    {
        /// <summary>
        /// The NuGet Gallery code should not use this constructor.
        /// </summary>
        public SupportRequestDbContext()
            : base("name=Gallery.SupportRequestSqlServer")
        {
        }

        /// <summary>
        /// The NuGet Gallery code should usually use this constructor, 
        /// so that we can configure the connection via the CLoud Service configuraton.
        /// </summary>
        public SupportRequestDbContext(string connectionString)
            : base(connectionString)
        {
        }

        public virtual IDbSet<Admin> Admins { get; set; }
        public virtual IDbSet<Issue> Issues { get; set; }
        public virtual IDbSet<IssueStatus> IssueStatus { get; set; }
        public virtual IDbSet<History> Histories { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Admin>()
                .Property(e => e.UserName)
                .IsUnicode(false);

            modelBuilder.Entity<History>()
               .Property(e => e.Comments)
               .IsUnicode(false);

            modelBuilder.Entity<History>()
                .Property(e => e.IssueStatus)
                .IsUnicode(false);

            modelBuilder.Entity<History>()
                .Property(e => e.AssignedTo)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                 .Property(e => e.CreatedBy)
                 .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.IssueTitle)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.Details)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.Comments)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.SiteRoot)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.PackageID)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.PackageVersion)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.OwnerEmail)
                .IsUnicode(false);

            modelBuilder.Entity<Issue>()
                .Property(e => e.Reason)
                .IsUnicode(false);

            modelBuilder.Entity<IssueStatus>()
                 .Property(e => e.StatusName)
                 .IsUnicode(false);
        }

        public void CommitChanges()
        {
            this.SaveChanges();
        }
    }
}
