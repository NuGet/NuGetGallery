DELETE FROM PackageReviews
DELETE FROM PackageDependencies
DELETE FROM PackageAuthors
DELETE FROM Packages
DELETE FROM PackageRegistrationOwners
DELETE FROM PackageRegistrations
DELETE FROM EmailMessages
DELETE FROM Users

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
  INSERT INTO Users ([Key], Username, HashedPassword, EmailAddress, ApiKey) VALUES (@key1, 'User'+@n1, 'hashedPassword', @n1+'@fnord.com', newid())
  INSERT INTO Users ([Key], Username, HashedPassword, EmailAddress, ApiKey) VALUES (@key2, 'User'+@n2, 'hashedPassword', @n2+'@fnord.com', newid())
  SET IDENTITY_INSERT Users OFF
  
  SET IDENTITY_INSERT PackageRegistrations ON
  INSERT INTO PackageRegistrations ([Key], Id) VALUES (@count, 'FnordReg'+@n)
  SET IDENTITY_INSERT PackageRegistrations OFF
  
  INSERT INTO PackageRegistrationOwners (UserKey, PackageRegistrationKey) VALUES (@key1, @count)
  INSERT INTO PackageRegistrationOwners (UserKey, PackageRegistrationKey) VALUES (@key2, @count)
  
  SET IDENTITY_INSERT Packages ON
  INSERT INTO Packages ([Key], PackageRegistrationKey, Version, Description, DownloadCount, Hash, HashAlgorithm, PackageFileSize, RequiresLicenseAcceptance, FlattenedAuthors, Published) VALUES (@key1, @count, '1.0', '1.0 Desc'+@n1, 0, '1.0 Hash'+@n1, '1.0 HashAlgoritm'+@n1, 8, 0, '1.0 FlattenedAuthors'+@n1, getdate())
  INSERT INTO Packages ([Key], PackageRegistrationKey, Version, Description, DownloadCount, Hash, HashAlgorithm, PackageFileSize, RequiresLicenseAcceptance, FlattenedAuthors, Published, IsLatest) VALUES (@key2, @count, '2.0', '2.0 Desc'+@n2, 0, '2.0 Hash'+@n2, '2.0 HashAlgoritm'+@n2, 8, 0, '2.0 FlattenedAuthors'+@n2, getdate(), 1)
  SET IDENTITY_INSERT Packages OFF
  
  SET IDENTITY_INSERT PackageAuthors ON
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key1, @key1, 'Author'+@n1)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key2, @key1, 'Author'+@n2)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key3, @key2, 'Author'+@n1)
  INSERT INTO PackageAuthors ([Key], PackageKey, Name) VALUES (@key4, @key2, 'Author'+@n2)
  SET IDENTITY_INSERT PackageAuthors OFF
  
  SET IDENTITY_INSERT PackageDependencies ON
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionRange) VALUES (@key1, @key1, 'Id'+@n1, 'Version'+@n2)
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionRange) VALUES (@key2, @key1, 'Id'+@n2, 'Version'+@n2)
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionRange) VALUES (@key3, @key2, 'Id'+@n1, 'Version'+@n2)
  INSERT INTO PackageDependencies ([Key], PackageKey, Id, VersionRange) VALUES (@key4, @key2, 'Id'+@n2, 'Version'+@n2)
  SET IDENTITY_INSERT PackageDependencies OFF
  
  SET IDENTITY_INSERT PackageReviews ON
  INSERT INTO PackageReviews ([Key], PackageKey, Rating, Review) VALUES (@key1, @key1, 1, 'Review'+@n2)
  INSERT INTO PackageReviews ([Key], PackageKey, Rating, Review) VALUES (@key2, @key1, 1, 'Review'+@n2)
  INSERT INTO PackageReviews ([Key], PackageKey, Rating, Review) VALUES (@key3, @key2, 1, 'Review'+@n2)
  INSERT INTO PackageReviews ([Key], PackageKey, Rating, Review) VALUES (@key4, @key2, 1, 'Review'+@n2)
  SET IDENTITY_INSERT PackageReviews OFF
  
  SET @count = (@count + 1)
END

UPDATE PackageRegistrations SET DownloadCount = 10 WHERE [Key] = 5000