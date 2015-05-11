// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCredentialDescriptionColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Credentials", "Identity", c => c.String(maxLength: 256));

            Sql(@"
                IF EXISTS (SELECT * FROM sys.views WHERE name = 'UsersAndCredentials')
                    DROP VIEW [dbo].[UsersAndCredentials]");
            Sql(@"
                CREATE VIEW UsersAndCredentials AS
                    SELECT u.Username, c.[Type], c.Value, c.[Identity]
                    FROM Users u
                    LEFT OUTER JOIN [Credentials] c ON c.UserKey = u.[Key]");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Credentials", "Identity");

            Sql(@"
                IF EXISTS (SELECT * FROM sys.views WHERE name = 'UsersAndCredentials')
                    DROP VIEW [dbo].[UsersAndCredentials]");
            Sql(@"
                CREATE VIEW UsersAndCredentials AS
                    SELECT u.Username, c.[Type], c.Value
                    FROM Users u
                    LEFT OUTER JOIN [Credentials] c ON c.UserKey = u.[Key]");
        }
    }
}
