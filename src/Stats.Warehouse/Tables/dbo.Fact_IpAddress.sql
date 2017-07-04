CREATE TABLE [dbo].[Fact_IpAddress]
(
    [Id]                INT				IDENTITY (1, 1) NOT NULL,
    [Address]			VARBINARY(16)	NOT NULL, 
    [TextAddress]		NVARCHAR(45)	NOT NULL, 
    CONSTRAINT [IpAddress] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Fact_IpAddress_UniqueIndex] ON [dbo].[Fact_IpAddress] ([TextAddress] ASC) INCLUDE ([Id])
GO

CREATE NONCLUSTERED INDEX [Fact_IpAddress_NCI_TextAddress]
    ON [dbo].[Fact_IpAddress]([TextAddress] ASC) WITH (STATISTICS_NORECOMPUTE = OFF);
GO

CREATE NONCLUSTERED INDEX [Fact_IpAddress_NCI_Address]
    ON [dbo].[Fact_IpAddress]([Address] ASC) WITH (STATISTICS_NORECOMPUTE = OFF);
GO