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
	DECLARE @count INT

	-- Ensure migration request exists
	SELECT @count = COUNT(*)
	FROM [dbo].[OrganizationMigrationRequests]
	WHERE NewOrganizationKey = @orgKey
		AND AdminUserKey = @adminKey
		AND ConfirmationToken = @token
	IF @count = 0 RETURN

	-- Ensure account is not member of other organizations
	SELECT @count = COUNT(*) FROM [dbo].[Memberships] WHERE MemberKey = @orgKey
	IF @count > 0 RETURN

	SELECT @count = COUNT(*) FROM [dbo].[MembershipRequests] WHERE NewMemberKey = @orgKey
	IF @count > 0 RETURN

	BEGIN TRANSACTION
	BEGIN TRY
		-- Change to Organization account with single admin membership
		INSERT INTO [dbo].[Organizations] ([Key]) VALUES (@orgKey)
		INSERT INTO [dbo].[Memberships] (OrganizationKey, MemberKey, IsAdmin) VALUES (@orgKey, @adminKey, 1)

		-- Remove organization credentials
		DELETE FROM [dbo].[Credentials] WHERE UserKey = @orgKey
    
		-- Delete the migration request
		DELETE FROM [dbo].[OrganizationMigrationRequests] WHERE NewOrganizationKey = @orgKey
  
		COMMIT TRANSACTION;
	END TRY
	BEGIN CATCH
		ROLLBACK TRANSACTION
	END CATCH
END