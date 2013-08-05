
SELECT Dimension_UserAgent.ClientMajorVersion, Dimension_UserAgent.ClientMinorVersion, SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_UserAgent ON Dimension_UserAgent.Id = Fact_Download.Dimension_UserAgent_Id
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE())
  AND Dimension_UserAgent.ClientCategory = 'NuGet'
  AND Dimension_UserAgent.ClientMajorVersion <= 2
  AND Dimension_UserAgent.ClientMinorVersion <= 7
GROUP BY Dimension_UserAgent.ClientMajorVersion, Dimension_UserAgent.ClientMinorVersion
ORDER BY Dimension_UserAgent.ClientMajorVersion, Dimension_UserAgent.ClientMinorVersion
