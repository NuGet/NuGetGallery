SET ANSI_NULLS ON

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AddPackageLicenseReport2]') AND type IN (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[AddPackageLicenseReport2]
END
