SELECT TOP(@RankingCount) Dimension_Package.PackageId, SUM(CASE WHEN Dimension_Operation.Operation = 'Install' THEN DownloadCount ELSE (0.5 * DownloadCount) END) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
INNER JOIN Dimension_Operation ON Dimension_Operation.Id = Fact_Download.Dimension_Operation_Id
INNER JOIN Dimension_Project ON Dimension_Project.Id = Fact_Download.Dimension_Project_Id
WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE())
  AND Dimension_Package.PackageListed = 1
  AND (Dimension_Operation.Operation = 'Install' OR Dimension_Operation.Operation = 'Update')
  AND Dimension_Project.ProjectTypes = @ProjectGuid
GROUP BY Dimension_Package.PackageId
ORDER BY Downloads DESC
