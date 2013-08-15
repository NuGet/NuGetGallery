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
     @reportUrl NVARCHAR(256) = NULL,
     @licenseNames LicenseNamesList READONLY,
     @comment NVARCHAR(256) = NULL,
	 @whatIf BIT
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

	DECLARE @returnTable TABLE
	(
		[Action] nvarchar(128),
		[Table] nvarchar(128),
		[Value] nvarchar(128)
	)
 
    -- Get package key
    
    SET @packageKey =
    (
        SELECT P.[Key]
        FROM   PackageRegistrations AS R
        JOIN   Packages AS P 
        ON     R.[Key] = P.PackageRegistrationKey
        WHERE  (R.Id = @packageId) AND (P.Version = @version)
    )

    IF @packageKey IS NULL
	BEGIN
		INSERT INTO @returnTable([Action], [Table], [Value]) VALUES('Error', 'Packages', 'Package not found: ' + @packageId + ' ' + @version)
		
		-- Return results
		SELECT -1 AS 'ReturnCode'
		SELECT * FROM @returnTable
	END
	ELSE
	BEGIN
    
		BEGIN TRANSACTION

			-- Add non-existing license names 
 			MERGE PackageLicenses
			USING @licenseNames l
			ON    PackageLicenses.Name = l.Name
			WHEN NOT MATCHED THEN
				INSERT (Name) VALUES (l.Name)
			OUTPUT $action AS 'Action', 'PackageLicenses' AS 'Table', INSERTED.Name AS 'Value' 
				INTO @returnTable([Action], [Table], Value);
    
			-- Get license names keys
 
			INSERT @licenseKeys
			SELECT PackageLicenses.[Key] FROM PackageLicenses 
			JOIN   @licenseNames l
			ON     PackageLicenses.Name = l.Name

			-- Is there already a report for this ID Version Sequence tuple? If so, delete it
			DELETE FROM PackageLicenseReports
				OUTPUT 'DELETE' AS 'Action', 'PackageLicenseReports' AS 'Table', deleted.[Key] AS 'Value'
					INTO @returnTable([Action], [Table], Value)
			WHERE PackageKey = @packageKey AND Sequence = @sequence

			-- Add report
			INSERT PackageLicenseReports (PackageKey, CreatedUtc, Sequence, ReportUrl, Comment)
				OUTPUT 'INSERT' AS 'Action', 'PackageLicenseReports' AS 'Table', CONVERT(nvarchar(64), INSERTED.[Key]) + ' URL:' + INSERTED.ReportUrl AS 'Value' 
					INTO @returnTable([Action], [Table], Value)
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
 
			DECLARE @pid TABLE (PackageKey INT NOT NULL PRIMARY KEY)
			UPDATE Packages
			SET    Packages.LicenseNames = @licenseNamesStr,
					Packages.LicenseReportUrl = @reportUrl
			OUTPUT INSERTED.[Key]
				INTO @pid(PackageKey)
			FROM   Packages p
				INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]
			WHERE  p.[Key] = @packageKey

			INSERT INTO @returnTable([Action], [Table], [Value])
			SELECT 'UPDATE' AS 'Action', 'Packages' AS 'Table', pr.Id + ' ' + p.[Version] + ': Report=' + p.LicenseReportUrl + ' Names=' + p.LicenseNames
			FROM @pid pid
				INNER JOIN Packages p ON pid.PackageKey = p.[Key]
				INNER JOIN PackageRegistrations pr ON p.PackageRegistrationKey = pr.[Key]

			-- Return results
			SELECT 0 AS 'ReturnCode'
			SELECT * FROM @returnTable
       
		IF @whatIf <> 1 COMMIT TRANSACTION ELSE ROLLBACK TRANSACTION
	END
END