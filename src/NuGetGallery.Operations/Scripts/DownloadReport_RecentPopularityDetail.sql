
SELECT TOP(500) 
	Dimension_Package.PackageId,
	Dimension_Package.PackageVersion,
	Dimension_Package.PackageTitle,
	Dimension_Package.PackageDescription,
	Dimension_Package.PackageIconUrl,
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
	Dimension_Package.PackageTitle,
	Dimension_Package.PackageDescription,
	Dimension_Package.PackageIconUrl
ORDER BY SUM(DownloadCount) DESC
