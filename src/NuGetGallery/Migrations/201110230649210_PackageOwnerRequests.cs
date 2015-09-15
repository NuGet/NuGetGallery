// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class PackageOwnerRequests : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "PackageOwnerRequests",
                c => new
                    {
                        Key = c.Int(nullable: false, identity: true),
                        PackageRegistrationKey = c.Int(nullable: false),
                        NewOwnerKey = c.Int(nullable: false),
                        RequestingOwnerKey = c.Int(nullable: false),
                        ConfirmationCode = c.String(),
                        RequestDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Key)
                .ForeignKey("PackageRegistrations", t => t.PackageRegistrationKey)
                .ForeignKey("Users", t => t.NewOwnerKey)
                .ForeignKey("Users", t => t.RequestingOwnerKey);
        }

        public override void Down()
        {
            DropForeignKey("PackageOwnerRequests", "PackageRegistrationKey", "PackageRegistrations");
            DropForeignKey("PackageOwnerRequests", "RequestingOwnerKey", "Users");
            DropForeignKey("PackageOwnerRequests", "NewOwnerKey", "Users");
            DropTable("PackageOwnerRequests");
        }
    }
}