CREATE TABLE [dbo].[Agg_PackageDownloads_LogFile]
(
	[Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [LogFileName] NVARCHAR(255) NOT NULL,
    [Dimension_Date_Id] INT NOT NULL,
    [PackageDownloads] INT NOT NULL,
    CONSTRAINT [FK_Agg_PackageDownloads_LogFile_Dimension_Date] FOREIGN KEY ([Dimension_Date_Id]) REFERENCES [dbo].[Dimension_Date]([Id])
)
