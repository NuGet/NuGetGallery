CREATE TABLE [dbo].[Support_TranslatePackages]
(
	[CorrectedPackageId]						INT NOT NULL ,
	[IncorrectPackageId]		NVARCHAR(255) NOT NULL,
	[IncorrectPackageVersion]	NVARCHAR(128) NOT NULL,

    CONSTRAINT [PK_Support_TranslatePackages] PRIMARY KEY CLUSTERED ([CorrectedPackageId]) WITH (STATISTICS_NORECOMPUTE = ON),
    CONSTRAINT [FK_Support_TranslatePackages_Dimension_Package] FOREIGN KEY ([CorrectedPackageId]) REFERENCES [Dimension_Package]([Id])
)
GO

CREATE INDEX [IX_Support_TranslatePackages] ON [dbo].[Support_TranslatePackages] ([IncorrectPackageId], [IncorrectPackageVersion]) INCLUDE ([CorrectedPackageId])
GO