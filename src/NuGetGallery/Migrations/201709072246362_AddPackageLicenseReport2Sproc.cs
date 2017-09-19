// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Migrations
{
    public partial class AddPackageLicenseReport2Sproc : SqlResourceMigration
    {
        public AddPackageLicenseReport2Sproc()
            : base("NuGetGallery.Infrastructure.AddPackageLicenseReport2.Up.sql", "NuGetGallery.Infrastructure.AddPackageLicenseReport2.Down.sql")
        {
        }
    }
}
