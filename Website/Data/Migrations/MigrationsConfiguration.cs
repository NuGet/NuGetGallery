using System.Data.Entity.Migrations;
using System.Linq;
using NuGetGallery.Data.Model;

namespace NuGetGallery.Data.Migrations
{
    public class MigrationsConfiguration : DbMigrationsConfiguration<EntitiesContext>
    {
        public MigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(EntitiesContext context)
        {
            var roles = context.Set<Role>();
            if (!roles.Any(x => x.Name == Constants.AdminRoleName))
            {
                roles.Add(new Role { Name = Constants.AdminRoleName });
                context.SaveChanges();
            }
        }
    }
}
