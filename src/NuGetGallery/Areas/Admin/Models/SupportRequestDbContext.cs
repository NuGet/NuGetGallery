namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Collections.Generic;

    public partial class SupportRequestDbContext :  DbContext, ISupportRequestDbContext
    {
        public SupportRequestDbContext()
            : base("name=SupportRequest")
        {
        }

        public virtual DbSet<Admin> Admins { get; set; }
        public virtual DbSet<Issue> Issues { get; set; }
        public virtual DbSet<IssueStatus> IssueStatus { get; set; }
        public virtual DbSet<History> Histories { get; set; }

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
