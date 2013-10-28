
/*  you can use this script manually against an existing warehouse
 *  for new warehouse instances, these schema changes have been incorporated into the create script 
 */

ALTER TABLE [dbo].[Dimension_Operation] ALTER COLUMN [Operation] NVARCHAR(128) NOT NULL;
GO

INSERT [dbo].[Dimension_Operation] VALUES ( 'Reinstall-Dependency' )
GO

