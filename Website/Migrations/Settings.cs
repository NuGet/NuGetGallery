using System.Linq;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Providers;
using System.Data.SqlClient;

namespace NuGetGallery.Migrations
{
    public class Settings : DbMigrationContext<EntitiesContext>
    {
        public Settings()
        {
            AutomaticMigrationsEnabled = false;
            SetCodeGenerator<CSharpMigrationCodeGenerator>();
            AddSqlGenerator<SqlConnection, SqlServerMigrationSqlGenerator>();
        }

        protected override void Seed(EntitiesContext context)
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
}
