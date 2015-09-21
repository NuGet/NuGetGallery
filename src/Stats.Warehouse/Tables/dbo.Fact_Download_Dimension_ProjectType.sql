CREATE TABLE [dbo].[Fact_Download_Dimension_ProjectType]
(
	[Fact_Download_Id]				UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES Fact_Download(Id),
	[Dimension_ProjectType_Id]		INT NOT NULL FOREIGN KEY REFERENCES Dimension_ProjectType(Id),
    CONSTRAINT [PK_Fact_Download_ProjectType] PRIMARY KEY ([Fact_Download_Id], [Dimension_ProjectType_Id])
)
