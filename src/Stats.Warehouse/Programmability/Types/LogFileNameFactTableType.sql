CREATE TYPE [dbo].[LogFileNameFactTableType] AS TABLE
(
	[LogFileName]			NVARCHAR(255)	NULL,
	INDEX IX_IP NONCLUSTERED ([LogFileName])
)
