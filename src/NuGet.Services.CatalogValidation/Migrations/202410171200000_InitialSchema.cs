// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.CatalogValidation.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialSchema : DbMigration
    {
        public override void Up()
        {
            // This migration will copy the schema from the validation database
            // The tables will be created by running the same migrations that were used for validation
            
            // Note: This is a placeholder migration. The actual implementation would:
            // 1. Copy all CREATE TABLE statements from the validation database
            // 2. Copy all CREATE INDEX statements from the validation database
            // 3. Copy all foreign key constraints from the validation database
            
            // For now, we'll create a minimal schema that can be expanded
            CreateTable(
                "dbo.PackageValidationSets",
                c => new
                {
                    Key = c.Int(nullable: false, identity: true),
                    ValidationTrackingId = c.Guid(nullable: false),
                    PackageKey = c.Int(),
                    PackageId = c.String(maxLength: 128),
                    PackageNormalizedVersion = c.String(maxLength: 64),
                    PackageContentType = c.String(maxLength: 128),
                    ValidationProperties = c.String(),
                    Created = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    Updated = c.DateTime(nullable: false, precision: 7, storeType: "datetime2"),
                    Expiration = c.DateTime(precision: 7, storeType: "datetime2"),
                    ValidationStatus = c.Int(nullable: false),
                    RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                })
                .PrimaryKey(t => t.Key);
        }
        
        public override void Down()
        {
            DropTable("dbo.PackageValidationSets");
        }
    }
}