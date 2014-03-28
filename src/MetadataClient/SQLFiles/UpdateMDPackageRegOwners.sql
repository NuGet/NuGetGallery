INSERT INTO dbo.MDPackageRegOwners
(PackageKey, [Hash])
	SELECT PackageRegistrationKey, UserKey
	FROM dbo.PackageRegistrationOwners
	WHERE NOT EXISTS
		(SELECT PackageRegistrationKey, UserKey
		 FROM dbo.MDPackageRegOwners)