INSERT INTO dbo.MDPackages
(PackageKey, [Hash])
	SELECT [Key], [Hash]
	FROM dbo.Packages
	WHERE [Key] NOT IN
		(SELECT PackageKey FROM dbo.MDPackages)