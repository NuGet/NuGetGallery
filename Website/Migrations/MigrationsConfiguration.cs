using System;
using System.Data.Entity.Migrations;
using System.Linq;

namespace NuGetGallery.Migrations
{
    public class MigrationsConfiguration : DbMigrationsConfiguration<EntitiesContext>
    {
        private const string GalleryOwnerEmail = "nugetgallery@outercurve.org";
        private const string GalleryOwnerName = "NuGet Gallery";

        public MigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(EntitiesContext context)
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
}
