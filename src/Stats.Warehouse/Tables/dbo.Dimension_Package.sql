CREATE TABLE [dbo].[Dimension_Package] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [PackageId]          NVARCHAR (255) NOT NULL,
    [PackageVersion]     NVARCHAR (128)  NOT NULL,
    [LowercasedPackageId] AS LOWER([PackageId]) PERSISTED,
	[LowercasedPackageVersion] AS LOWER([PackageVersion]) PERSISTED,
    CONSTRAINT [PK_Dimension_Package] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Package_NCI_PackageId_PackageVersion]
    ON [dbo].[Dimension_Package]([PackageId] ASC, [PackageVersion] ASC) WITH (STATISTICS_NORECOMPUTE = OFF);
GO

CREATE NONCLUSTERED INDEX [Dimension_Package_Lowercased]
	ON [dbo].[Dimension_Package]([LowercasedPackageId] ASC, [LowercasedPackageVersion] ASC) INCLUDE ([Id], [PackageId], [PackageVersion]) WITH (STATISTICS_NORECOMPUTE = OFF);
GO