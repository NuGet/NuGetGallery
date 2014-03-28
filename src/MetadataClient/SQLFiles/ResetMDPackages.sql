DELETE FROM dbo.MDPackages
INSERT INTO dbo.MDPackages
(PackageKey, [Hash])
SELECT [Key], [Hash]
FROM dbo.Packages