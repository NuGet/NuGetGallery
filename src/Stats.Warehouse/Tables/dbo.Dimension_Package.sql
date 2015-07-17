CREATE TABLE [dbo].[Dimension_Package] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [PackageId]          NVARCHAR (128) NULL,
    [PackageVersion]     NVARCHAR (64)  NULL,
    [PackageListed]      BIT            DEFAULT ((1)) NOT NULL,
    [PackageTitle]       NVARCHAR (256) NULL,
    [PackageDescription] NVARCHAR (MAX) NULL,
    [PackageIconUrl]     NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_Dimension_Package] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Package_2]
    ON [dbo].[Dimension_Package]([PackageId] ASC, [PackageVersion] ASC) WITH (STATISTICS_NORECOMPUTE = ON);


GO
CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Package_NCI_PackageId_PackageVersion]
    ON [dbo].[Dimension_Package]([PackageId] ASC, [PackageVersion] ASC) WITH (STATISTICS_NORECOMPUTE = ON);