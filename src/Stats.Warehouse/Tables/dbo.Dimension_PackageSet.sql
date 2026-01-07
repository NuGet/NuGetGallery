CREATE TABLE [dbo].[Dimension_PackageSet]
(
	[Id]					INT				IDENTITY (1, 1) NOT NULL,
	[Name]					NVARCHAR(128)	NOT NULL,
	CONSTRAINT [PK_Dimension_PackageSet] PRIMARY KEY CLUSTERED ([Id] ASC) WITH (STATISTICS_NORECOMPUTE = OFF)
);
GO
CREATE NONCLUSTERED INDEX [Dimension_PackageSet]
    ON [dbo].[Dimension_PackageSet] ([Id])
	INCLUDE ([Name])  WITH (STATISTICS_NORECOMPUTE = OFF);
GO