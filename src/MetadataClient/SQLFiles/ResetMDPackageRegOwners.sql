DELETE FROM dbo.MDPackageRegOwners
INSERT INTO dbo.MDPackageRegOwners
(PackageRegistrationKey, UserKey)
SELECT PackageRegistrationKey, UserKey
FROM dbo.PackageRegistrationOwners