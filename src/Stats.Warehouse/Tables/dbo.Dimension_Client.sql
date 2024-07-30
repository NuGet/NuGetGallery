CREATE TABLE [dbo].[Dimension_Client]
(
	[Id]					INT				IDENTITY (1, 1) NOT NULL,
    [ClientName]			VARCHAR (128)	NULL,
    [Major]					VARCHAR(50)		NULL,
    [Minor]					VARCHAR(50)		NULL,
    [Patch]					VARCHAR(50)		NULL,
    [ClientVersion]  AS     (concat(isnull([Major],'0'),'.',isnull([Minor],'0'),'.',isnull([Patch],'0'))) PERSISTED NOT NULL,
    [ClientCategory] AS     ([dbo].[GetClientCategory]([ClientName])) PERSISTED,
    CONSTRAINT [PK_Dimension_Client] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Dimension_Client_UniqueIndex] ON [dbo].[Dimension_Client] ([ClientName] ASC, [Major] DESC, [Minor] DESC, [Patch] DESC)
INCLUDE ([Id], [ClientVersion], [ClientCategory])
WITH (STATISTICS_NORECOMPUTE = OFF)
GO

CREATE INDEX [IX_Dimension_Client_ClientCategory] ON [dbo].[Dimension_Client] ([ClientCategory])
INCLUDE ([Id],[Major])
WITH (STATISTICS_NORECOMPUTE = OFF)
GO