DELETE FROM dbo.MDPackageState
INSERT INTO dbo.MDPackageState
(PackageKey, Id, [Version], LastEdited)
SELECT T1.[Key], Id, [Version], LastEdited
FROM dbo.Packages T1
INNER JOIN dbo.PackageRegistrations T2
ON T1.[PackageRegistrationKey] = T2.[Key]