CREATE TYPE [dbo].[ToolDimensionTableType] AS TABLE
(
	[ToolId]			NVARCHAR(255)	NOT NULL,
	[ToolVersion]		NVARCHAR(128)	NOT NULL,
	[FileName]			NVARCHAR(128)	NOT NULL,
	UNIQUE NONCLUSTERED ([ToolId], [ToolVersion], [FileName])
)