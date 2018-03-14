-- Copyright (c) .NET Foundation. All rights reserved.
-- Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

SET ANSI_NULLS ON

-- Transform User into Organization account. Must be done with inline SQL because EF does not support changing
-- types for entities that use inheritance.

DECLARE @requestCount INT

SELECT @requestCount = COUNT(*)
FROM  [dbo].[OrganizationMigrationRequests]
WHERE NewOrganizationKey = @organizationKey
	AND AdminUserKey = @adminKey
	AND ConfirmationToken = @token

IF @requestCount <= 0 RETURN

BEGIN TRANSACTION
BEGIN TRY
	-- Change to Organization account with single admin membership
	INSERT INTO [dbo].[Organizations] ([Key]) VALUES (@organizationKey)
	INSERT INTO [dbo].[Memberships] (OrganizationKey, MemberKey, IsAdmin) VALUES (@organizationKey, @adminKey, 1)

	-- Reassign organization API keys to the admin user
	-- Only reassign scoped keys (not full access keys)
	UPDATE [dbo].[Scopes] SET OwnerKey = @organizationKey
	WHERE CredentialKey IN (
		SELECT [Key] FROM [dbo].[Credentials]
		WHERE UserKey = @organizationKey AND Type LIKE 'apikey.%'
	)
	UPDATE [dbo].[Credentials] SET UserKey = @adminKey
	FROM [dbo].[Credentials] AS C
	JOIN [dbo].[Scopes] AS S ON S.CredentialKey = C.[Key]
	WHERE C.UserKey = @organizationKey AND C.Type LIKE 'apikey.%'

	-- Remove remaining organization credentials
	DELETE FROM [dbo].[Credentials] WHERE UserKey = @organizationKey
    
	-- Delete the migration request
	DELETE FROM [dbo].[OrganizationMigrationRequests] WHERE NewOrganizationKey = @organizationKey
  
	COMMIT TRANSACTION;
END TRY
BEGIN CATCH
	ROLLBACK TRANSACTION
END CATCH