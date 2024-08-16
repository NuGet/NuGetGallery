CREATE TABLE [dbo].[Dimension_Platform]
(
	[Id]                INT				IDENTITY (1, 1) NOT NULL,
    [OSFamily]          VARCHAR (128)	NULL,
	[Major]				VARCHAR(50)		NULL,
    [Minor]				VARCHAR(50)		NULL,
    [Patch]				VARCHAR(50)		NULL,
    [PatchMinor]		VARCHAR(50)		NULL,
	[OSVersion] AS CONCAT(ISNULL([Major], '0'), '.', ISNULL([Minor], '0'), '.', ISNULL([Patch], '0'), '.', ISNULL([PatchMinor], '0')),
    CONSTRAINT [PK_Dimension_Platform] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Dimension_Platform_UniqueIndex] ON [dbo].[Dimension_Platform] ([OSFamily] ASC, [Major] DESC, [Minor] DESC, [Patch] DESC, [PatchMinor] DESC) INCLUDE ([Id], [OSVersion])
GO