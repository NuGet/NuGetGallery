
SELECT Dimension_Date.[Year], Dimension_Date.MonthOfYear, SUM(DownloadCount) 'Downloads'
FROM Fact_Download
INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
WHERE Dimension_Date.[Date] >= 
	DATETIMEFROMPARTS(
		DATEPART(year, DATEADD(month, -7, GETDATE())),
		DATEPART(month, DATEADD(month, -7, GETDATE())),
		1, 0, 0, 0, 0)
AND Dimension_Date.[Date] <
	DATETIMEFROMPARTS(
		DATEPART(year, GETDATE()),
		DATEPART(month, GETDATE()),
		1, 0, 0, 0, 0)
GROUP BY Dimension_Date.[Year], Dimension_Date.MonthOfYear
ORDER BY Dimension_Date.[Year], Dimension_Date.MonthOfYear
