// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddMinRequiredVerisonColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("Packages", "MinClientVersion", c => c.String(maxLength: 44));
        }
        
        public override void Down()
        {
            DropColumn("Packages", "MinClientVersion");
        }
    }
}
