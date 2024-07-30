CREATE TABLE [dbo].[Fact_Package_PackageSet] (
    [Id]                      INT            IDENTITY (1, 1) NOT NULL,
    [Dimension_PackageSet_Id] INT            NOT NULL,
    [LowercasedPackageId]     NVARCHAR (255) NOT NULL,
    [IsPrimary]               BIT            DEFAULT (0) NOT NULL,
    CONSTRAINT [PK_Fact_Package_PackageSet] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF),
    CONSTRAINT [FK_Fact_Package_PackageSet_Dimension_PackageSet] FOREIGN KEY ([Dimension_PackageSet_Id]) REFERENCES [dbo].[Dimension_PackageSet] ([Id])
);

