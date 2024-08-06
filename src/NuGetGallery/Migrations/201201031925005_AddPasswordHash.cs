// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Data.Entity.Migrations;

namespace NuGetGallery.Migrations
{
    public partial class AddPasswordHash : DbMigration
    {
        public override void Up()
        {
            AddColumn("Users", "PasswordHashAlgorithm", c => c.String(nullable: false, defaultValue: "SHA1"));
        }

        public override void Down()
        {
            DropColumn("Users", "PasswordHashAlgorithm");
        }
    }
}