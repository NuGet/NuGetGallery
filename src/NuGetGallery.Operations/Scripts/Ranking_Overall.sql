
SELECT Dimension_Package.PackageId, SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
INNER JOIN Dimension_Operation ON Dimension_Operation.Id = Fact_Download.Dimension_Operation_Id
WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE())
  AND Dimension_Package.PackageListed = 1
  AND Dimension_Operation.Operation = 'Install'
GROUP BY Dimension_Package.PackageId
ORDER BY SUM(DownloadCount) DESC
