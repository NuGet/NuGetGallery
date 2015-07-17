CREATE TABLE [dbo].[Dimension_Client]
(
	[Id]                 INT           IDENTITY (1, 1) NOT NULL,
    [ClientName]         VARCHAR (128) NULL,
    [Major] VARCHAR(50)           NULL,
    [Minor] VARCHAR(50)           NULL,
    [Patch] VARCHAR(50) NULL,
    CONSTRAINT [PK_Dimension_Client] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Dimension_Client_UniqueIndex] ON [dbo].[Dimension_Client] ([ClientName] ASC, [Major] DESC, [Minor] DESC, [Patch] DESC) INCLUDE ([Id])
GO