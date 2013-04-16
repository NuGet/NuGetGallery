
SELECT TOP(500) 
	Dimension_Package.PackageId 'PackageId',
	Dimension_Package.PackageVersion 'PackageVersion',
	ISNULL(Dimension_Package.PackageTitle, '') 'PackageTitle',
	ISNULL(Dimension_Package.PackageDescription, '') 'PackageDescription',
	ISNULL(Dimension_Package.PackageIconUrl, '') 'PackageIconUrl',
	SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE())
  AND Dimension_Package.PackageListed = 1
GROUP BY 
	Dimension_Package.PackageId, 
	Dimension_Package.PackageVersion,
	ISNULL(Dimension_Package.PackageTitle, ''),
	ISNULL(Dimension_Package.PackageDescription, ''),
	ISNULL(Dimension_Package.PackageIconUrl, '')
ORDER BY SUM(DownloadCount) DESC
