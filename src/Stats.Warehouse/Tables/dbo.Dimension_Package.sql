CREATE TABLE [dbo].[Dimension_Package] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [PackageId]          NVARCHAR (128) NOT NULL,
    [PackageVersion]     NVARCHAR (128)  NOT NULL,
    CONSTRAINT [PK_Dimension_Package] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = ON)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [Dimension_Package_NCI_PackageId_PackageVersion]
    ON [dbo].[Dimension_Package]([PackageId] ASC, [PackageVersion] ASC) WITH (STATISTICS_NORECOMPUTE = ON);