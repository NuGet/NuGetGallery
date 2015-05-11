// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class ListPackagesIndexes : DbMigration
    {
        public override void Up()
        {
            // CreateIndex doesn't support INCLUDE, so just use raw SQL
            Sql("CREATE NONCLUSTERED INDEX [IX_PackageAuthors_PackageKey] ON [dbo].[PackageAuthors] ([PackageKey]) INCLUDE ([Key],[Name])");
            CreateIndex(table: "Packages", column: "IsLatestStable", name: "IX_Packages_IsLatestStable");
        }

        public override void Down()
        {
            DropIndex(table: "Packages", name: "IX_Packages_IsLatestStable");
            DropIndex(table: "PackageAuthors", name: "IX_PackageAuthors_PackageKey");
        }
    }
}