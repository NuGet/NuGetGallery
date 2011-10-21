using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Providers;
using System.Data.SqlClient;
using System.Linq;

namespace NuGetGallery.Migrations
{
    public class Settings : DbMigrationContext<MigrationsContext>
    {
        public Settings()
        {
            AutomaticMigrationsEnabled = false;
            SetCodeGenerator<CSharpMigrationCodeGenerator>();
            AddSqlGenerator<SqlConnection, SqlServerMigrationSqlGenerator>();
        }

        protected override void Seed(MigrationsContext context)
        {
            var roles = context.Set<Role>();
            var adminRole = roles.Where(x => x.Name == Const.AdminRoleName).SingleOrDefault();
            if (adminRole == null)
            {
                adminRole = new Role() { Name = Const.AdminRoleName };
                roles.Add(adminRole);
                context.SaveChanges();
            }
        }
    }

    // This is a hack to work around the hack we had to use in EntitieContext to get it to work with MiniProfiler.
    // When EF 4.2 is out, we can switch to MiniProfiler.EF and remove all of the hacks (or, so we're told).
    public class MigrationsContext : EntitiesContext
    {
        public MigrationsContext()
            : base("NuGetGallery")
        {
        }
    }
}
