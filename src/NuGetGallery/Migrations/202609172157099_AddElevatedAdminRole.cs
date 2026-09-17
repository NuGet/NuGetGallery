// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddElevatedAdminRole : DbMigration
    {
        public override void Up()
        {
            Sql(@"
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = N'ElevatedAdmin')
BEGIN
    INSERT INTO dbo.Roles (Name) VALUES (N'ElevatedAdmin');
END");
        }

        public override void Down()
        {
            Sql(@"
DELETE ur
FROM dbo.UserRoles AS ur
INNER JOIN dbo.Roles AS r ON r.[Key] = ur.RoleKey
WHERE r.Name = N'ElevatedAdmin';

DELETE FROM dbo.Roles WHERE Name = N'ElevatedAdmin';");
        }
    }
}
