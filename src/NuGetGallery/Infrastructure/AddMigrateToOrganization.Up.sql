-- Copyright (c) .NET Foundation. All rights reserved.
-- Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

SET ANSI_NULLS ON

IF OBJECT_ID('[dbo].[MigrateToOrganization]', 'P') IS NOT NULL
  DROP PROCEDURE  [dbo].[MigrateToOrganization]
GO

CREATE PROCEDURE [dbo].[MigrateToOrganization]
(
  @orgKey INT,
  @adminKey INT,
  @token NVARCHAR(MAX)
)
AS
BEGIN
  DECLARE @reqCount INT

  -- Ensure migration request exists
  SELECT @reqCount = COUNT(*)
  FROM [dbo].[OrganizationMigrationRequests]
  WHERE NewOrganizationKey = @orgKey
	AND AdminUserKey = @adminKey
	AND ConfirmationToken = @token
  IF @reqCount = 0 RETURN (0)

  BEGIN TRANSACTION
  BEGIN TRY
    -- Ensure Organizations do not have credentials or memberships
    DELETE FROM [dbo].[Credentials] WHERE UserKey = @orgKey
    DELETE FROM [dbo].[Memberships] WHERE MemberKey = @orgKey
    DELETE FROM [dbo].[MembershipRequests] WHERE NewMemberKey = @orgKey
    
    -- Change to Organization account with single admin membership
    INSERT INTO [dbo].[Organizations] ([Key]) VALUES (@orgKey)
    INSERT INTO [dbo].[Memberships] (OrganizationKey, MemberKey, IsAdmin) VALUES (@orgKey, @adminKey, 1)
    
    -- Delete the migration request
    DELETE FROM [dbo].[OrganizationMigrationRequests] WHERE NewOrganizationKey = @orgKey
  
    COMMIT TRANSACTION;
	RETURN (1)
  END TRY
  BEGIN CATCH
    ROLLBACK TRANSACTION
	RETURN (0)
  END CATCH
END