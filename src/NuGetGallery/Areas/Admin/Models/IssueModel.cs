namespace NuGetGallery.Areas.Admin.Models
{ 
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Collections.Generic;

    public partial class IssueModel : DbContext
    {
        public virtual DbSet<Issue> Issues { get; set; }
        public AdminModel admin = new AdminModel();
        public IssueStatusModel issueStatus = new IssueStatusModel();

        public IssueModel()
            : base("name=Gallery.SupportRequestDB")
        {
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
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
        }

        public List<Issue> GetOpenIssues()
        {
            var allOpenIssues = from r in Issues
                                where (r.IssueStatus != 5 && r.IssueStatus != 6)
                                select r;
            return allOpenIssues.ToList();
        }

        public List<Issue> GetResolvedIssues()
        {
            var allResolvedIssues = from r in Issues
                                    where (r.IssueStatus == 5 || r.IssueStatus == 6)
                                    select r;
            return allResolvedIssues.ToList();
        }

        public List<Issue> GetAllIssues()
        {
            var allIssues = from r in Issues
                            select r;
            return allIssues.ToList();
        }

        public List<Issue> GetUnassignedIssues()
        {
            var allUnassignedIssues = from r in Issues
                                      where (r.AssignedTo == 0)
                                      select r;
            return allUnassignedIssues.ToList();
        }

        public int GetCountOfUnassignedIssues()
        {
            var allIssues = from r in Issues
                            where (r.AssignedTo == 0)
                            select r;
            var count = 0;
            if (allIssues != null)
            {
                count = allIssues.Count();
            }
            return count;
        }

        public Issue GetIssueById(int id)
        {
            var issue = from r in Issues
                        where (r.Id == id)
                        select r;
            if (issue.Count() > 0)
            {
                return issue.First();
            }
            else
            {
                return null;
            }
        }

        public int GetCountOfOpenIssues()
        {
            var count = (from r in Issues
                         where (r.IssueStatus != 5 && r.IssueStatus != 6)
                         select r).Count();
            return count;
        }

        public int GetCountOfResolvedIssues()
        {
            var count = (from r in Issues
                         where (r.IssueStatus == 5 || r.IssueStatus == 6)
                         select r).Count();
            return count;
        }

        public void AddIssue(Issue newIssue)
        {
            Issues.Add(newIssue);
            SaveChanges();

            AddHistoryEntry(newIssue);
        }

        public void AddHistoryEntry(Issue newIssue)
        {
            var history = new HistoryModel();
            var newEntry = new History();
            newEntry.EntryDate = DateTime.Now;
            newEntry.EditedBy = 1;
            newEntry.AssignedTo = admin.GetUserNameById(newIssue.AssignedTo);
            newEntry.IssueStatus = issueStatus.GetIssueStatusNameById(newIssue.IssueStatus);
            newEntry.IssueKey = newIssue.Id;
            newEntry.Comments = newIssue.Comments;
            history.AddNewHistoryEntry(newEntry);
        }
    }
}