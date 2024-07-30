CREATE TYPE [dbo].[ClientDimensionTableType] AS TABLE
(
	[UserAgent]			NVARCHAR(MAX)	NULL,
	[ClientName]        VARCHAR(128)	NULL,
	[Major]				VARCHAR(50)		NULL,
    [Minor]				VARCHAR(50)		NULL,
    [Patch]				VARCHAR(50)		NULL
)
