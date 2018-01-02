// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Migrations
{
    public partial class AddMigrateToOrganizationSproc : SqlResourceMigration
    {
        public AddMigrateToOrganizationSproc()
            : base(
                  "NuGetGallery.Infrastructure.AddMigrateToOrganization.Up.sql",
                  "NuGetGallery.Infrastructure.AddMigrateToOrganization.Down.sql"
                  )
        {
        }
    }
}