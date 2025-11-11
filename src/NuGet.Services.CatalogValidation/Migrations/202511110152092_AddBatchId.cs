// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.CatalogValidation
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddBatchId : DbMigration
    {
        public override void Up()
        {
            // Check if BatchId column exists before adding it
            Sql(@"
				IF NOT EXISTS (
					SELECT * FROM sys.columns 
					WHERE object_id = OBJECT_ID(N'dbo.ValidatorStatuses') 
					AND name = 'BatchId'
				)
				BEGIN
					ALTER TABLE [dbo].[ValidatorStatuses] ADD [BatchId] [nvarchar](256)
				END
			");

            // Check if Discriminator column exists before adding it
            Sql(@"
				IF NOT EXISTS (
					SELECT * FROM sys.columns 
					WHERE object_id = OBJECT_ID(N'dbo.ValidatorStatuses') 
					AND name = 'Discriminator'
				)
				BEGIN
					ALTER TABLE [dbo].[ValidatorStatuses] ADD [Discriminator] [nvarchar](128) NOT NULL DEFAULT 'CatalogValidatorStatus'
				END
			");

            // Update any existing rows to have the CatalogValidatorStatus discriminator value
            Sql(@"
				UPDATE [dbo].[ValidatorStatuses]
				SET [Discriminator] = 'CatalogValidatorStatus'
				WHERE [Discriminator] IS NULL OR [Discriminator] = ''
			");
        }

        public override void Down()
        {
            DropColumn("dbo.ValidatorStatuses", "Discriminator");
            DropColumn("dbo.ValidatorStatuses", "BatchId");
        }
    }
}
