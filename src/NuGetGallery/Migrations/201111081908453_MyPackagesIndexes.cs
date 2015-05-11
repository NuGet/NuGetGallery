// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class MyPackagesIndexes : DbMigration
    {
        public override void Up()
        {
            // CreateIndex doesn't support INCLUDE
            Sql(
                "CREATE NONCLUSTERED INDEX [IX_PackageRegistrationOwners_UserKey] ON [dbo].[PackageRegistrationOwners] ([UserKey]) INCLUDE ([PackageRegistrationKey])");
        }

        public override void Down()
        {
            DropIndex(table: "PackageRegistrationOwners", name: "IX_PackageRegistrationOwners_UserKey");
        }
    }
}