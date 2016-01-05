namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Collections.Generic;

    public partial class IssueStatusModel : DbContext
    {
        public IssueStatusModel()
            : base("name=IssueModel")
        {
        }

        public virtual DbSet<IssueStatus> IssueStatuses { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IssueStatus>()
                .Property(e => e.StatusName)
                .IsUnicode(false);
        }

        public List<IssueStatus> GetAllIssueStatuses()
        {
            var statuses = from s in IssueStatuses
                           select s;
            return statuses.ToList();
        }

        public IssueStatus GetIssueStatusById(int id)
        {
            var status = from r in IssueStatuses
                         where (r.Id == id)
                         select r;

            if (status.Count() > 0)
            {
                return status.First();
            }
            else
            {
                return null;
            }
        }

        public string GetIssueStatusNameById(int id)
        {
            var status = from r in IssueStatuses
                         where (r.Id == id)
                         select r.StatusName;

            if (status.Count() > 0)
            {
                return status.First();
            }
            else
            {
                return null;
            }
        }

        public int GetIssueStatusIdByName(string issueStatusName)
        {
            if (String.IsNullOrEmpty(issueStatusName))
                return -1;

            var id = from r in IssueStatuses
                     where (r.StatusName == issueStatusName)
                     select r.Id;

            if (id.Count() > 0)
            {
                return id.First();
            }
            else
            {
                return -1;
            }
        }
    }
}