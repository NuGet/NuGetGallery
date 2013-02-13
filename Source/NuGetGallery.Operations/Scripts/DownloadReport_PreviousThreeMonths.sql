SELECT TOP(500) Dimension_Package.PackageId, Dimension_Package.PackageVersion, SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
WHERE Dimension_Date.[Date] >= 
	DATETIMEFROMPARTS(
		DATEPART(year, DATEADD(month, -3, GETDATE())),
		DATEPART(month, DATEADD(month, -3, GETDATE())),
		1, 0, 0, 0, 0)
AND Dimension_Date.[Date] <
	DATETIMEFROMPARTS(
		DATEPART(year, GETDATE()),
		DATEPART(month, GETDATE()),
		1, 0, 0, 0, 0)
GROUP BY Dimension_Package.PackageId, Dimension_Package.PackageVersion
ORDER BY SUM(DownloadCount) DESC
