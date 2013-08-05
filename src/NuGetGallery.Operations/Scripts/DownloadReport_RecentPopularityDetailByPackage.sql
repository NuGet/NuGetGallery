SELECT 
	Dimension_Package.PackageVersion, 
	Dimension_UserAgent.ClientCategory,
	Dimension_UserAgent.Client,
	Dimension_UserAgent.ClientMajorVersion,
	Dimension_UserAgent.ClientMinorVersion,
	Dimension_Operation.Operation, 
	SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
INNER JOIN Dimension_Operation ON Dimension_Operation.Id = Fact_Download.Dimension_Operation_Id
INNER JOIN Dimension_UserAgent ON Dimension_UserAgent.Id = Fact_Download.Dimension_UserAgent_Id
WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE())
  AND Dimension_Package.PackageId = @PackageId
GROUP BY 
	Dimension_Package.PackageVersion, 
	Dimension_UserAgent.Client,
	Dimension_UserAgent.ClientCategory,
	Dimension_UserAgent.ClientMajorVersion,
	Dimension_UserAgent.ClientMinorVersion,
	Dimension_Operation.Operation
ORDER BY 
	Dimension_Package.PackageVersion, 
	Dimension_UserAgent.Client,
	Dimension_UserAgent.ClientCategory,
	Dimension_UserAgent.ClientMajorVersion,
	Dimension_UserAgent.ClientMinorVersion,
	Dimension_Operation.Operation,
	SUM(DownloadCount) DESC
