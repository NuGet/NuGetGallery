using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Providers;
using System.Data.SqlClient;
using System.Linq;

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
            if (!roles.Any(x => x.Name == Const.AdminRoleName))
            {
                roles.Add(new Role() { Name = Const.AdminRoleName });
                context.SaveChanges();
            }

            var gallerySettings = context.Set<GallerySetting>();
            if (!gallerySettings.Any())
            {
                gallerySettings.Add(new GallerySetting
                {
                    SmtpHost = null,
                    SmtpPort = null
                });
                context.SaveChanges();
            }
        }
    }
}
