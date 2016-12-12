DECLARE @rawVersionTable TABLE (Version_Attributes XML, FullVersionString nvarchar(256))
DECLARE @VersionTableIntPatch TABLE (FullVersionString nvarchar(256), Major int, Minor int, Patch int, Tag nvarchar(256), Other nvarchar(256))
DECLARE @VersionTableStrPatch TABLE (FullVersionString nvarchar(256), Major int, Minor int, Patch nvarchar(256), Tag nvarchar(256), Other nvarchar(256))
DECLARE @LatestVersion nvarchar(256)
DECLARE @PackageKey int
DECLARE @RegistrationKey int

SET @RegistrationKey = 1

Update Packages
set IsLatest = 0
WHERE PackageRegistrationKey = @RegistrationKey

INSERT INTO @rawVersionTable
SELECT
    CONVERT(XML,'<Version><Attribute>' 
        + REPLACE(REPLACE([NormalizedVersion],'.', '</Attribute><Attribute>'), '-', '</Attribute><Attribute>')
        + '</Attribute></Version>') AS Version_Attributes,
	[NormalizedVersion] AS FullVersionString
FROM Packages
where PackageRegistrationKey = @RegistrationKey and Deleted = 0 and Listed = 1


BEGIN TRY
Insert INTO @VersionTableIntPatch
SELECT
	FullVersionString,
    CONVERT(int, Version_Attributes.value('/Version[1]/Attribute[1]','varchar(25)')) AS [Major],
    CONVERT(int, Version_Attributes.value('/Version[1]/Attribute[2]','varchar(25)')) AS [Minor],
    CONVERT(int, Version_Attributes.value('/Version[1]/Attribute[3]','varchar(25)')) AS [Patch],
    Version_Attributes.value('/Version[1]/Attribute[4]','varchar(25)') AS [Tag],
    Version_Attributes.value('/Version[1]/Attribute[5]','varchar(25)') AS [Other]
FROM @rawVersionTable

SET @LatestVersion = (SELECT TOP 1 FullVersionString FROM @VersionTableIntPatch
ORDER BY Major DESC, Minor DESC, Patch DESC, Tag DESC)

UPDATE Packages SET IsLatest = 1 WHERE NormalizedVersion = @LatestVersion and PackageRegistrationKey = @RegistrationKey
END TRY
BEGIN CATCH
BEGIN TRY
INSERT INTO @VersionTableStrPatch
SELECT
	FullVersionString,
    CONVERT(int, Version_Attributes.value('/Version[1]/Attribute[1]','varchar(25)')) AS [Major],
    CONVERT(int, Version_Attributes.value('/Version[1]/Attribute[2]','varchar(25)')) AS [Minor],
    Version_Attributes.value('/Version[1]/Attribute[3]','varchar(25)') AS [Patch],
    Version_Attributes.value('/Version[1]/Attribute[4]','varchar(25)') AS [Tag],
    Version_Attributes.value('/Version[1]/Attribute[5]','varchar(25)') AS [Other]
FROM @rawVersionTable

SET @LatestVersion = (SELECT TOP 1 FullVersionString FROM @VersionTableStrPatch
ORDER BY Major DESC, Minor DESC, Patch DESC, Tag DESC)

UPDATE Packages SET IsLatest = 1 WHERE NormalizedVersion = @LatestVersion and PackageRegistrationKey = @RegistrationKey
END TRY
BEGIN CATCH
SET @LatestVersion = '';
END CATCH
END CATCH
GO
