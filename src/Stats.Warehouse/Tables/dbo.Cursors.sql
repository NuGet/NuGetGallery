CREATE TABLE [dbo].[Cursors]
(
	[Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT newid(),
    [Name] NVARCHAR(128) NOT NULL,
    [Position] DATETIME NOT NULL
)

GO

CREATE UNIQUE INDEX [IX_Cursors] ON [dbo].[Cursors] ([Name]) INCLUDE ([Position])
