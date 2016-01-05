namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Collections.Generic;

    public partial class HistoryModel : DbContext
    {
        public HistoryModel()
            : base("name=IssueModel")
        {
        }

        public virtual DbSet<History> Histories { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<History>()
                .Property(e => e.Comments)
                .IsUnicode(false);

            modelBuilder.Entity<History>()
                .Property(e => e.IssueStatus)
                .IsUnicode(false);

            modelBuilder.Entity<History>()
                .Property(e => e.AssignedTo)
                .IsUnicode(false);
        }

        public List<History> GetHistoryEntriesByIssueKey(int id)
        {
            var entries = from a in Histories
                          where a.IssueKey == id
                          select a;

            if (entries.Count() > 0)
            {
                return entries.ToList();
            }
            else
            {
                return null;
            }
        }

        public void AddNewHistoryEntry(History newEntry)
        {
            Histories.Add(newEntry);
            SaveChanges();
        }
    }
}