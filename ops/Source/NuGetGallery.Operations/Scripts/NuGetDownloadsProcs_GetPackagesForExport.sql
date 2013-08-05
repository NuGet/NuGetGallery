

IF OBJECT_ID('[dbo].[GetPackagesForExport]') IS NOT NULL
    DROP PROCEDURE [dbo].[GetPackagesForExport]
GO

CREATE PROCEDURE [dbo].[GetPackagesForExport]
AS
BEGIN
    SELECT PackageId, DirtyCount
    FROM PackageReportDirty
END
GO

