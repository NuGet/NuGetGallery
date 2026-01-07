CREATE TYPE [dbo].[LogFileAggregatesPackageDownloadsByDateTableType] AS TABLE
(
	LogFileName NVARCHAR(255) NOT NULL,
	Dimension_Date_Id INT NOT NULL,
	PackageDownloads INT NOT NULL
)