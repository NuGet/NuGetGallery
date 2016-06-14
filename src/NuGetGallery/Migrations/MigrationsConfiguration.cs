// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Migrations;
using System.Linq;

namespace NuGetGallery.Migrations
{
    public class MigrationsConfiguration : DbMigrationsConfiguration<EntitiesContext>
    {
        public MigrationsConfiguration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(EntitiesContext context)
        {
            // Ensure roles exist
            var roles = context.Set<Role>();
            if (!roles.Any(x => x.Name == Constants.AdminRoleName))
            {
                roles.Add(new Role { Name = Constants.AdminRoleName });
                context.SaveChanges();
            }
            if (!roles.Any(x => x.Name == Constants.DelegatedRoleName))
            {
                roles.Add(new Role { Name = Constants.DelegatedRoleName });
                context.SaveChanges();
            }

            // Ensure "microsoft" user is in delegated role 
            var user = context.Set<User>()
                .Include("Roles")
                .FirstOrDefault(u => u.Username == "microsoft");
            if (user != null && !user.IsInRole(Constants.DelegatedRoleName))
            {
                user.Roles.Add(roles.First(r => r.Name == Constants.DelegatedRoleName));
            }

            // Ensure settings exist
            var gallerySettings = context.Set<GallerySetting>();
            if (!gallerySettings.Any())
            {
                gallerySettings.Add(new GallerySetting { Key = 1 });
                context.SaveChanges();
            }
        }
    }
}
