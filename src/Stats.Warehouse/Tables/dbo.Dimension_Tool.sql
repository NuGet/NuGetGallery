CREATE TABLE [dbo].[Dimension_Tool]
(
	[Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [ToolId]          NVARCHAR (255) NOT NULL,
    [ToolVersion]     NVARCHAR (128)  NOT NULL,
    [FileName]     NVARCHAR (128)  NOT NULL,
    [LowercasedToolId] AS LOWER([ToolId]) PERSISTED,
	[LowercasedToolVersion] AS LOWER([ToolVersion]) PERSISTED,
	[LowercasedFileName] AS LOWER([FileName]) PERSISTED,
    CONSTRAINT [PK_Dimension_Tool] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Tool_NCI_ToolId_ToolVersion_FileName]
    ON [dbo].[Dimension_Tool]([ToolId] ASC, [ToolVersion] ASC, [FileName] ASC) WITH (STATISTICS_NORECOMPUTE = OFF);
GO

CREATE NONCLUSTERED INDEX [Dimension_Tool_Lowercased]
	ON [dbo].[Dimension_Tool]([LowercasedToolId] ASC, [LowercasedToolVersion] ASC, [LowercasedFileName] ASC) INCLUDE ([Id], [ToolId], [ToolVersion], [FileName]) WITH (STATISTICS_NORECOMPUTE = OFF);
GO