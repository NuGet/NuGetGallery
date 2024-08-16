CREATE TYPE [dbo].[IpAddressFactTableType] AS TABLE
(
    [Address]			VARBINARY(16)	NOT NULL,
    [TextAddress]		NVARCHAR(45)	NOT NULL,
	INDEX IX_IP NONCLUSTERED ([Address], [TextAddress])
)
