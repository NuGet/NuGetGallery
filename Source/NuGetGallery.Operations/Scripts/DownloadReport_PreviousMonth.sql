
SELECT TOP(500) Dimension_Package.PackageId, Dimension_Package.PackageVersion, SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
WHERE Dimension_Date.[MonthOfYear] = DATEPART(month, DATEADD(month, -1, GETDATE()))
  AND Dimension_Date.[Year] = DATEPART(year, DATEADD(month, -1, GETDATE()))
GROUP BY Dimension_Package.PackageId, Dimension_Package.PackageVersion
ORDER BY SUM(DownloadCount) DESC
