SET ANSI_NULLS ON

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AddPackageLicenseReport]') AND type IN (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[AddPackageLicenseReport]
END
GO

IF TYPE_ID(N'[dbo].[LicenseNamesList]') IS NOT NULL
    DROP TYPE [dbo].[LicenseNamesList]
GO

--EXEC dbo.sp_executesql @statement = N'
CREATE TYPE [dbo].[LicenseNamesList] AS TABLE
(
     Name VARCHAR(128) NOT NULL PRIMARY KEY
)
--'
GO

CREATE PROCEDURE [dbo].[AddPackageLicenseReport]
(
     @sequence INT,
     @packageId NVARCHAR(128),
     @version NVARCHAR(64),
     @reportUrl NVARCHAR(256),
     @licenseNames LicenseNamesList READONLY,
     @comment NVARCHAR(256)
 )
 AS
 BEGIN
	
    SET NOCOUNT ON 

    DECLARE @reportKey       INT
    DECLARE @packageKey      INT
    DECLARE @licenseNamesStr VARCHAR(MAX)
    DECLARE @licenseKeys     TABLE
    (
        [Key] INT NOT NULL PRIMARY KEY
    )

    -- Check input

    IF @sequence  IS NULL RETURN
    IF @packageId IS NULL RETURN
    IF @version   IS NULL RETURN
 
    -- Get package key
    
    SET @packageKey =
    (
        SELECT P.[Key]
        FROM   PackageRegistrations AS R
        JOIN   Packages AS P 
        ON     R.[Key] = P.PackageRegistrationKey
        WHERE  (R.Id = @packageId) AND (P.Version = @version)
    )

    IF @packageKey IS NULL RETURN 
    
    BEGIN TRANSACTION
 
        -- Add non-existing license names 
 
        MERGE PackageLicenses
        USING @licenseNames l
        ON    PackageLicenses.Name = l.Name
        WHEN NOT MATCHED THEN
            INSERT (Name) VALUES (l.Name);
    
        -- Get license names keys
 
        INSERT @licenseKeys
        SELECT PackageLicenses.[Key] FROM PackageLicenses 
        JOIN   @licenseNames l
        ON     PackageLicenses.Name = l.Name
         
        -- Add report 
 
        INSERT PackageLicenseReports (PackageKey, CreatedUtc, Sequence, ReportUrl, Comment)
        VALUES (@packageKey, GETDATE(), @sequence, @reportUrl, @Comment)

		SELECT @reportKey = SCOPE_IDENTITY()
 
        -- Create relationship between report and licenses names 
 
        -- INSERT PackageLicenseReportLicenses (ReportKey, LicenseKey)
		INSERT PackageLicenseReportLicenses
		SELECT @reportKey AS ReportKey, [Key] FROM @licenseKeys

        -- Add denormalized data for optimization 
    
        -- Creates a comma-separated list
        SELECT @licenseNamesStr = COALESCE(@licenseNamesStr + ',', '') + Name 
        FROM   @licenseNames
 
        UPDATE Packages
        SET    Packages.LicenseNames = @licenseNamesStr,
               Packages.LicenseReportUrl = @reportUrl
        WHERE  Packages.[Key] = @packageKey
        
    COMMIT TRANSACTION
END