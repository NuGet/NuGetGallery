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
            SeedDatabase(context);
        }

        /// This method is a workaround to the fact that the Seed method 
        /// never seems to get called. So we'll try calling this manually later.
        public static void SeedDatabase(EntitiesContext context)
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
