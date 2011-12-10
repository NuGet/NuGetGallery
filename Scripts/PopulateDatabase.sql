USE NuGetGallery
GO

BEGIN TRAN

DECLARE		@PkgReg TABLE([Key] int unique)
DECLARE		@Pkg TABLE([Key] int unique)
DECLARE		@Usr TABLE([Key] int unique)

INSERT		@PkgReg
SELECT		[Key]
FROM		PackageRegistrations
WHERE		Id LIKE 'FnordReg%'

INSERT		@Pkg
SELECT		[Key]
FROM		Packages
WHERE		PackageRegistrationKey IN (SELECT [Key] FROM @PkgReg)

INSERT		@Usr
SELECT		[Key]
FROM		Users
WHERE		Username LIKE 'FnordUser%' OR EmailAddress LIKE '%fnord.com'

DELETE PackageDependencies			FROM PackageDependencies WHERE PackageKey IN (SELECT [Key] FROM @Pkg)
DELETE PackageOwnerRequests			FROM PackageOwnerRequests WHERE PackageRegistrationKey IN (SELECT [Key] FROM @PkgReg)
DELETE PackageAuthors				FROM PackageAuthors WHERE PackageKey IN (SELECT [Key] FROM @Pkg)
DELETE PackageStatistics			FROM PackageStatistics WHERE PackageKey IN (SELECT [Key] FROM @Pkg)
DELETE Packages						WHERE [Key] IN (SELECT [Key] FROM @Pkg)
DELETE PackageRegistrationOwners	FROM PackageRegistrationOwners WHERE PackageRegistrationKey IN (SELECT [Key] FROM @PkgReg)
DELETE PackageRegistrations			WHERE [Key] IN (SELECT [Key] FROM @PkgReg)
DELETE UserRoles					WHERE UserKey IN (SELECT [Key] FROM @Usr)
DELETE Users						WHERE [Key] IN (SELECT [Key] FROM @Usr)

DECLARE @count INT
SET @count = 1
WHILE (@count <= 5000)
BEGIN
  DECLARE @n VARCHAR(6), @n1 VARCHAR(6), @n2 VARCHAR(6)
  DECLARE @key1 INT, @key2 INT, @key3 INT, @key4 INT
  SET @key1 = 1+((@count-1)*4)
  SET @key2 = 2+((@count-1)*4)
  SET @key3 = 3+((@count-1)*4)
  SET @key4 = 4+((@count-1)*4)
  SET @n = CONVERT(varchar(6), @count)
  SET @n1 = CONVERT(varchar(6), @key1)
  SET @n2 = CONVERT(varchar(6), @key2)
  
  
  SET IDENTITY_INSERT Users ON
  INSERT INTO Users ([Key], Username, HashedPassword, EmailAddress, ApiKey, EmailAllowed) VALUES (@key1, 'FnordUser'+@n1, 'hashedPassword', @n1+'@fnord.com', newid(), 1)
  INSERT INTO Users ([Key], Username, HashedPassword, EmailAddress, ApiKey, EmailAllowed) VALUES (@key2, 'FnordUser'+@n2, 'hashedPassword', @n2+'@fnord.com', newid(), 1)
  SET IDENTITY_INSERT Users OFF
  
  SET IDENTITY_INSERT PackageRegistrations ON
  INSERT INTO PackageRegistrations ([Key], Id) VALUES (@count, 'FnordReg'+@n)
  SET IDENTITY_INSERT PackageRegistrations OFF
  
  INSERT INTO PackageRegistrationOwners (UserKey, PackageRegistrationKey) VALUES (@key1, @count)
  INSERT INTO PackageRegistrationOwners (UserKey, PackageRegistrationKey) VALUES (@key2, @count)
  
  SET IDENTITY_INSERT Packages ON
  INSERT INTO Packages ([Key], PackageRegistrationKey, Version, Description, DownloadCount, Hash, HashAlgorithm, PackageFileSize, RequiresLicenseAcceptance, FlattenedAuthors, Published, Created, LastUpdated, IsLatest, IsLatestStable, Listed) VALUES (@key1, @count, '1.0', '1.0 Desc'+@n1, 0, '1.0 Hash'+@n1, '1.0 HashAlgoritm'+@n1, 8, 0, '1.0 FlattenedAuthors'+@n1, getdate(), getdate(), getdate(), 0, 0, 1)
  INSERT INTO Packages ([Key], PackageRegistrationKey, Version, Description, DownloadCount, Hash, HashAlgorithm, PackageFileSize, RequiresLicenseAcceptance, FlattenedAuthors, Published, Created, LastUpdated, IsLatest, IsLatestStable, Listed) VALUES (@key2, @count, '2.0', '2.0 Desc'+@n2, 0, '2.0 Hash'+@n2, '2.0 HashAlgoritm'+@n2, 8, 0, '2.0 FlattenedAuthors'+@n2, getdate(), getdate(), getdate(), 1, 1, 1)
  SET IDENTITY_INSERT Packages OFF
  
  SET IDENTITY_INSERT PackageAuthors ON
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key1, @key1, 'Author'+@n1)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key2, @key1, 'Author'+@n2)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key3, @key2, 'Author'+@n1)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key4, @key2, 'Author'+@n2)
  SET IDENTITY_INSERT PackageAuthors OFF
  
  SET IDENTITY_INSERT PackageDependencies ON
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionSpec) VALUES (@key1, @key1, 'Id'+@n1, '1.0')
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionSpec) VALUES (@key2, @key1, 'Id'+@n2, '2.0')
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionSpec) VALUES (@key3, @key2, 'Id'+@n1, '3.0')
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionSpec) VALUES (@key4, @key2, 'Id'+@n2, '4.0')
  SET IDENTITY_INSERT PackageDependencies OFF
  
  SET @count = (@count + 1)
END

UPDATE PackageRegistrations SET DownloadCount = 10 WHERE [Key] = 5000

COMMIT TRAN