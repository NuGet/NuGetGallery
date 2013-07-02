
IF OBJECT_ID('[dbo].[AddDownloadFact]') IS NOT NULL
    DROP PROCEDURE [dbo].[AddDownloadFact]
GO

CREATE PROCEDURE [dbo].[AddDownloadFact]
@PackageId NVARCHAR(128),
@PackageVersion NVARCHAR(64),
@PackageListed INT,
@PackageTitle NVARCHAR(256),
@PackageDescription NVARCHAR(MAX),
@PackageIconUrl NVARCHAR(MAX),
@DownloadUserAgent NVARCHAR(MAX),
@DownloadOperation NVARCHAR(18),
@DownloadTimestamp DATETIME,
@DownloadProjectTypes NVARCHAR(MAX),
@DownloadDependentPackageId NVARCHAR(128),
@OriginalKey INT
AS
BEGIN
    IF EXISTS (SELECT * FROM ReplicationMarker WHERE @OriginalKey <= LastOriginalKey)
        RETURN 0

    BEGIN TRAN

        -- you should be only able to add if the OriginalKey is greater than the max-original-key
        -- lower key values have no effect but return success - making this proc idempotent
        -- this presumes an incrementing external id in the nugetgallery database

        DECLARE @Dimension_PackageId INT;

        SELECT @Dimension_PackageId = Id
        FROM Dimension_Package
        WHERE PackageId = @PackageId
          AND PackageVersion = @PackageVersion;

        IF (@Dimension_PackageId IS NULL)
        BEGIN
            INSERT Dimension_Package
            (
                PackageId,
                PackageVersion,
                PackageListed,
                PackageTitle,
                PackageDescription,
                PackageIconUrl
            )
            VALUES 
            (
                @PackageId,
                @PackageVersion,
                @PackageListed,
                @PackageTitle,
                @PackageDescription,
                @PackageIconUrl
            );

            SELECT @Dimension_PackageId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE Dimension_Package
            SET
                PackageListed = @PackageListed,
                PackageTitle = @PackageTitle,
                PackageDescription = @PackageDescription,
                PackageIconUrl = @PackageIconUrl
            WHERE Id = @Dimension_PackageId
        END

        DECLARE @Dimension_UserAgentId INT;

        SELECT @Dimension_UserAgentId = Id
        FROM Dimension_UserAgent
        WHERE Value = @DownloadUserAgent;

        IF (@Dimension_UserAgentId IS NULL)
        BEGIN
            INSERT Dimension_UserAgent 
            ( 
                Value,
                Client,
                ClientMajorVersion,
                ClientMinorVersion,
                ClientCategory
            )
            SELECT 
                @DownloadUserAgent,
                [dbo].[UserAgentClient](@DownloadUserAgent),
                [dbo].[UserAgentClientMajorVersion](@DownloadUserAgent),
                [dbo].[UserAgentClientMinorVersion](@DownloadUserAgent),
                [dbo].[UserAgentClientCategory](@DownloadUserAgent)

            SELECT @Dimension_UserAgentId = SCOPE_IDENTITY();
        END

        DECLARE @Dimension_DateId INT;

        SELECT @Dimension_DateId = [Id]
        FROM [dbo].[Dimension_Date]
        WHERE [Date] = CAST(@DownloadTimestamp AS DATE);

        DECLARE @Dimension_TimeId INT;

        SELECT @Dimension_TimeId = Id
        FROM Dimension_Time
        WHERE HourOfDay = DATEPART(HOUR, @DownloadTimestamp);

        DECLARE @Dimension_OperationId INT;

        SELECT @Dimension_OperationId = Id
        FROM Dimension_Operation
        WHERE Operation = @DownloadOperation;

        IF (@Dimension_OperationId IS NULL)
        BEGIN
            SELECT @Dimension_OperationId = Id
            FROM Dimension_Operation
            WHERE Operation = '(unknown)';
        END

        DECLARE @Dimension_ProjectId INT;

        IF (@DownloadProjectTypes IS NULL)
        BEGIN
            SELECT @DownloadProjectTypes = '(unknown)';
        END

        SELECT @Dimension_ProjectId = Id
        FROM Dimension_Project
        WHERE ProjectTypes = @DownloadProjectTypes;

        IF (@Dimension_ProjectId IS NULL)
        BEGIN
            INSERT Dimension_Project
            (
                ProjectTypes
            )
            VALUES
            (
                @DownloadProjectTypes
            );

            SELECT @Dimension_ProjectId = SCOPE_IDENTITY();
        END

        IF EXISTS (SELECT * FROM Fact_Download
            WHERE Dimension_Package_Id = @Dimension_PackageId
              AND Dimension_UserAgent_Id = @Dimension_UserAgentId
              AND Dimension_Date_Id = @Dimension_DateId
              AND Dimension_Time_Id = @Dimension_TimeId
              AND Dimension_Operation_Id = @Dimension_OperationId
              AND Dimension_Project_Id = @Dimension_ProjectId)
        BEGIN
            UPDATE Fact_Download
            SET DownloadCount = DownloadCount + 1
            WHERE Dimension_Package_Id = @Dimension_PackageId
              AND Dimension_UserAgent_Id = @Dimension_UserAgentId
              AND Dimension_Date_Id = @Dimension_DateId
              AND Dimension_Time_Id = @Dimension_TimeId
              AND Dimension_Operation_Id = @Dimension_OperationId
              AND Dimension_Project_Id = @Dimension_ProjectId
        END
        ELSE
        BEGIN
            INSERT INTO Fact_Download 
            (
                Dimension_Package_Id,
                Dimension_UserAgent_Id, 
                Dimension_Date_Id, 
                Dimension_Time_Id,
                Dimension_Operation_Id,
                Dimension_Project_Id,
                DownloadCount
            )
            VALUES
            (
                @Dimension_PackageId,
                @Dimension_UserAgentId,
                @Dimension_DateId,
                @Dimension_TimeId,
                @Dimension_OperationId,
                @Dimension_ProjectId,
                1
            )
        END

        DELETE ReplicationMarker;

        INSERT INTO ReplicationMarker ( LastOriginalKey ) VALUES ( @OriginalKey );

        IF EXISTS (SELECT * FROM PackageReportDirty WHERE PackageId = @PackageId)
        BEGIN
            UPDATE PackageReportDirty
            SET DirtyCount = DirtyCount + 1
            WHERE PackageId = @PackageId 			
        END
        ELSE
        BEGIN
            INSERT PackageReportDirty ( PackageId, DirtyCount ) VALUES ( @PackageId, 1 )
        END

    COMMIT TRAN
END
GO

