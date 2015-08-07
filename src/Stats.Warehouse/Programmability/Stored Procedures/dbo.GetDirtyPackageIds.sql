CREATE PROCEDURE [dbo].[GetDirtyPackageIds]
AS
BEGIN
	SET NOCOUNT ON;

	-- Get last known cursor position
	DECLARE @CursorPosition DATETIME = ISNULL((SELECT [Position] FROM [dbo].[Cursors] WHERE [Name] = 'GetDirtyPackageId'), CONVERT(DATETIME, '1900-01-01'))

	-- Run to second latest timestamp in facts table
	DECLARE @CursorRunToPosition DATETIME = (
												SELECT	MAX([Timestamp])
												FROM	[dbo].[Fact_Download] (NOLOCK)
												WHERE	[Timestamp] < (SELECT MAX([Timestamp]) FROM [dbo].[Fact_Download] (NOLOCK) )
											 )

	-- query for dirty package id's
	-- dirty = package id has registered new downloads between last known cursor position (exclusive) and cursor run-to position (inclusive)

	SELECT	DISTINCT P.[PackageId], @CursorRunToPosition AS [CursorRunToPosition]
	FROM	[dbo].[Fact_Download] (NOLOCK) AS F

	INNER JOIN	[dbo].[Dimension_Package] AS P (NOLOCK)
	ON			P.[Id] = F.[Dimension_Package_Id]

	INNER JOIN	[dbo].[Dimension_Date] AS D (NOLOCK)
	ON			D.[Id] = F.[Dimension_Date_Id]

	WHERE	D.[Date] > @CursorPosition AND D.[Date] <= @CursorRunToPosition

	ORDER BY P.[PackageId] ASC
END