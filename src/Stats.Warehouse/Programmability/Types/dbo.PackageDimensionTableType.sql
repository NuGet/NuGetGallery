CREATE TYPE [dbo].[PackageDimensionTableType] AS TABLE
(
	[PackageId]			NVARCHAR(255)	NOT NULL,
	[PackageVersion]    NVARCHAR(128)	NOT NULL,
	UNIQUE NONCLUSTERED ([PackageId], [PackageVersion])
)
