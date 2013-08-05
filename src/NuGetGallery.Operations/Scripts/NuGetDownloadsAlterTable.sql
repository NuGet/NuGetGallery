
/*  you can use this script manually against an existing warehouse
 *  for new warehouse instances, these schema changes have been incorporated into the create script 
 */

ALTER TABLE [dbo].[Dimension_Package] ADD [PackageListed] BIT NOT NULL DEFAULT 1
GO

ALTER TABLE [dbo].[Dimension_Package] ADD [PackageTitle] NVARCHAR(256)
GO

ALTER TABLE [dbo].[Dimension_Package] ADD [PackageDescription] NVARCHAR(MAX)
GO

ALTER TABLE [dbo].[Dimension_Package] ADD [PackageIconUrl] NVARCHAR(MAX)
GO

