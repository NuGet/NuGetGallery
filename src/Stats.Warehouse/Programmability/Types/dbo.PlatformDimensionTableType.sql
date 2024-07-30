CREATE TYPE [dbo].[PlatformDimensionTableType] AS TABLE
(
	[UserAgent]			NVARCHAR(MAX)	NULL,
	[OSFamily]          VARCHAR(128)	NULL,
	[Major]				VARCHAR(50)		NULL,
    [Minor]				VARCHAR(50)		NULL,
    [Patch]				VARCHAR(50)		NULL,
    [PatchMinor]		VARCHAR(50)		NULL
)
