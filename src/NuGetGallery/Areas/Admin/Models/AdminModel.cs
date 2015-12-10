namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Collections.Generic;

    public partial class AdminModel : DbContext
    {
        public AdminModel()
            : base("name=IssueModel")
        {
        }

        public virtual DbSet<Admin> Admins { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Admin>()
                .Property(e => e.UserName)
                .IsUnicode(false);
        }

        public List<Admin> GetAllAdmins()
        {
            var names = from a in Admins
                        select a;

            return names.ToList();
        }

        public List<String> GetAllUserNames()
        {
            var names = from a in Admins
                        select (a.UserName);

            return names.ToList();
        }

        public string GetUserNameById(int id)
        {
            var name = from a in Admins
                       where a.Id == id
                       select (a.UserName);

            if (name != null && name.Count() > 0)
            {
                return name.First();
            }
            else
            {
                return null;
            }
        }

        public int GetAdminKeyFromUserName(string userName)
        {
            var id = from a in Admins
                       where userName.Equals(a.UserName, StringComparison.OrdinalIgnoreCase)
                       select (a.Id);

            if (id != null && id.Count() > 0)
            {
                return id.First();
            }
            else
            {
                return 0;
            }
        }
    }
}
