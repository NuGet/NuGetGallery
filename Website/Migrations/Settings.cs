using System;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Providers;
using System.Data.SqlClient;
using System.Linq;

namespace NuGetGallery.Migrations
{
    public class Settings : DbMigrationContext<MigrationsContext>
    {
        private const string GalleryOwnerEmail = "nugetgallery@outercurve.org";
        private const string GalleryOwnerName = "NuGet Gallery";

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
            if (!roles.Any(x => x.Name == Constants.AdminRoleName))
            {
                roles.Add(new Role() { Name = Constants.AdminRoleName });
                context.SaveChanges();
            }

            var gallerySettings = context.Set<GallerySetting>();
            if (!gallerySettings.Any())
            {
                gallerySettings.Add(new GallerySetting
                {
                    SmtpHost = null,
                    SmtpPort = null,
                    GalleryOwnerEmail = GalleryOwnerEmail,
                    GalleryOwnerName = GalleryOwnerName,
                    ConfirmEmailAddresses = true
                });
                context.SaveChanges();
            }
            else
            {
                var gallerySetting = gallerySettings.First();
                if (String.IsNullOrEmpty(gallerySetting.GalleryOwnerEmail))
                {
                    gallerySetting.GalleryOwnerEmail = GalleryOwnerEmail;
                }
                if (String.IsNullOrEmpty(gallerySetting.GalleryOwnerName))
                {
                    gallerySetting.GalleryOwnerName = GalleryOwnerName;
                }
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
