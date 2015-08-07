CREATE PROCEDURE [dbo].[DownloadReportListInactive]
AS
BEGIN
	SET NOCOUNT ON;

		SELECT	DISTINCT [PackageId]
		FROM	[dbo].[Fact_Download] AS F

		-- INNER JOIN with Downloads to ensure we do not remove newly inserted package id's
		INNER JOIN	Dimension_Package AS P
		ON			P.[Id] = F.[Dimension_Package_Id]


	EXCEPT

		SELECT	DISTINCT P.PackageId
		FROM	[dbo].[Fact_Download] AS F

		INNER JOIN	Dimension_Package AS P
		ON			P.[Id] = F.[Dimension_Package_Id]

		INNER JOIN	Dimension_Date AS D
		ON			D.[Id] = F.[Dimension_Date_Id]

		WHERE		D.[Date] IS NOT NULL
				AND ISNULL(D.[Date], CONVERT(DATE, '1900-01-01')) >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
				AND ISNULL(D.[Date], CONVERT(DATE, DATEADD(day, 1, GETDATE()))) < CONVERT(DATE, GETDATE())

END