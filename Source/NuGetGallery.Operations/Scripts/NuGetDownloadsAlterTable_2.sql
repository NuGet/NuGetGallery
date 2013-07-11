
/*  you can use this script manually against an existing warehouse
 *  for new warehouse instances, these schema changes have been incorporated into the create script 
 */

IF OBJECT_ID('[dbo].[Dimension_Project]') IS NULL
    CREATE TABLE [dbo].[Dimension_Project]
    (
        [Id] INT IDENTITY,
        [ProjectTypes] NVARCHAR(450)
        CONSTRAINT [PK_Dimension_Project] PRIMARY KEY CLUSTERED ( [Id] )
    )
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'Dimension_Project_NCI_ProjectTypes')
    CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Project_NCI_ProjectTypes] ON [Dimension_Project] ( [ProjectTypes] )
GO

INSERT [dbo].[Dimension_Project] VALUES ( '(unknown)' )

DROP INDEX Dimension_Operation_NCI_Operation ON [dbo].[Dimension_Operation]
GO

ALTER TABLE [dbo].[Dimension_Operation] ALTER COLUMN [Operation] NVARCHAR(18) NOT NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Operation_NCI_Operation] ON [Dimension_Operation] ( [Operation] )
GO

INSERT [dbo].[Dimension_Operation] VALUES ( 'Install-Dependency' )
INSERT [dbo].[Dimension_Operation] VALUES ( 'Update-Dependency' )
INSERT [dbo].[Dimension_Operation] VALUES ( 'Restore-Dependency' )
GO

/* DEFAULT 1 */
ALTER TABLE [dbo].[Fact_Download] ADD [Dimension_Project_Id] INT NOT NULL;
GO

