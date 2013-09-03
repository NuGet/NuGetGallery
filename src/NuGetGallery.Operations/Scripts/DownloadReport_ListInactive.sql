
SELECT DISTINCT Dimension_Package.PackageId
FROM Dimension_Package
WHERE Dimension_Package.PackageId NOT IN (
	SELECT DISTINCT Dimension_Package.PackageId
	FROM Fact_Download
	INNER JOIN Dimension_Package ON Dimension_Package.Id = Fact_Download.Dimension_Package_Id
	INNER JOIN Dimension_Date ON Dimension_Date.Id = Fact_Download.Dimension_Date_Id
	WHERE Dimension_Date.[Date] >= CONVERT(DATE, DATEADD(day, -42, GETDATE()))
	  AND Dimension_Date.[Date] < CONVERT(DATE, GETDATE()))
