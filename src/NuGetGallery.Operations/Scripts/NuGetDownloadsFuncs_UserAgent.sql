

IF OBJECT_ID('[dbo].[UserAgentClient]') IS NOT NULL
    DROP FUNCTION [dbo].[UserAgentClient]
GO

CREATE FUNCTION [dbo].[UserAgentClient] (@value nvarchar(900))
RETURNS NVARCHAR(128)
AS
BEGIN
	-- NUGET CLIENTS

	-- VS NuGet 2.8+ User Agent Strings
	IF CHARINDEX('NuGet VS PowerShell Console/', @value) > 0
		RETURN 'NuGet VS PowerShell Console'
	IF CHARINDEX('NuGet VS Packages Dialog - Solution/', @value) > 0
		RETURN 'NuGet VS Packages Dialog - Solution'
	IF CHARINDEX('NuGet VS Packages Dialog/', @value) > 0
		RETURN 'NuGet VS Packages Dialog'	
	
	-- VS NuGet (pre-2.8) User Agent Strings
    IF CHARINDEX('NuGet Add Package Dialog/', @value) > 0 
        RETURN 'NuGet Add Package Dialog'
    IF CHARINDEX('NuGet Command Line/', @value) > 0 
        RETURN 'NuGet Command Line'
    IF CHARINDEX('NuGet Package Manager Console/', @value) > 0 
        RETURN 'NuGet Package Manager Console'
    IF CHARINDEX('NuGet Visual Studio Extension/', @value) > 0 
        RETURN 'NuGet Visual Studio Extension'
    IF CHARINDEX('Package-Installer/', @value) > 0 
        RETURN 'Package-Installer'
    
		-- WebMatrix includes its own core version number as part of the client name, before the slash
		-- Therefore we don't include the slash in the match
    IF CHARINDEX('WebMatrix', @value) > 0 
        RETURN 'WebMatrix'
	
    -- ECOSYSTEM PARTNERS

	-- Refer to npe.codeplex.com
    IF CHARINDEX('NuGet Package Explorer Metro/', @value) > 0 
        RETURN 'NuGet Package Explorer Metro'
    IF CHARINDEX('NuGet Package Explorer/', @value) > 0 
        RETURN 'NuGet Package Explorer'

	-- Refer to www.jetbrains.com for details
	-- TeamCity uses a space to separate the client from the version instead of a /
    IF CHARINDEX('JetBrains TeamCity ', @value) > 0 
        RETURN 'JetBrains TeamCity'

	-- Refer to www.sonatype.com for details
	-- Make sure to use the slash here because there are "Nexus" phones that match otherwise
    IF CHARINDEX('Nexus/', @value) > 0 
        RETURN 'Sonatype Nexus'

	-- Refer to www.jfrog.com for details
    IF CHARINDEX('Artifactory/', @value) > 0 
        RETURN 'JFrog Artifactory'

	-- Refer to www.myget.org
	-- MyGet doesn't send a version at all, so be sure to omit the /
    IF CHARINDEX('MyGet', @value) > 0 
        RETURN 'MyGet'
        
	-- Refer to www.inedo.com for details
    IF CHARINDEX('ProGet/', @value) > 0 
        RETURN 'Inedo ProGet'        
    
    RETURN 'Other'
END
GO

IF OBJECT_ID('[dbo].[UserAgentClientMajorVersion]') IS NOT NULL
    DROP FUNCTION [dbo].[UserAgentClientMajorVersion]
GO

CREATE FUNCTION [dbo].[UserAgentClientMajorVersion] (@value NVARCHAR(900))
RETURNS INT
AS
BEGIN
    IF	(
			-- VS NuGet 2.8+ User Agent Strings
			CHARINDEX('NuGet VS PowerShell Console/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog - Solution/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog/', @value) > 0

			-- VS NuGet (pre-2.8) User Agent Strings
		OR	CHARINDEX('NuGet Add Package Dialog/', @value) > 0
        OR	CHARINDEX('NuGet Command Line/', @value) > 0
        OR	CHARINDEX('NuGet Package Manager Console/', @value) > 0
        OR	CHARINDEX('NuGet Visual Studio Extension/', @value) > 0
        OR	CHARINDEX('Package-Installer/', @value) > 0

			-- WebMatrix NuGet User Agent String
        OR	CHARINDEX('WebMatrix', @value) > 0

			-- NuGet Package Explorer
		OR	CHARINDEX('NuGet Package Explorer Metro/', @value) > 0
        OR	CHARINDEX('NuGet Package Explorer/', @value) > 0
		)
        
        RETURN CAST(SUBSTRING(
            @value, 
            CHARINDEX('/', @value) + 1,
            CHARINDEX('.', @value, CHARINDEX('/', @value) + 1) - (CHARINDEX('/', @value) + 1)
        ) AS INT)

    RETURN 0
END
GO

IF OBJECT_ID('[dbo].[UserAgentClientMinorVersion]') IS NOT NULL
    DROP FUNCTION [dbo].[UserAgentClientMinorVersion]
GO

CREATE FUNCTION [dbo].[UserAgentClientMinorVersion] (@value NVARCHAR(900))
RETURNS INT
AS
BEGIN
    IF	(
			-- VS NuGet 2.8+ User Agent Strings
			CHARINDEX('NuGet VS PowerShell Console/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog - Solution/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog/', @value) > 0

			-- VS NuGet (pre-2.8) User Agent Strings
		OR	CHARINDEX('NuGet Add Package Dialog/', @value) > 0
        OR	CHARINDEX('NuGet Command Line/', @value) > 0
        OR	CHARINDEX('NuGet Package Manager Console/', @value) > 0
        OR	CHARINDEX('NuGet Visual Studio Extension/', @value) > 0
        OR	CHARINDEX('Package-Installer/', @value) > 0

			-- WebMatrix includes its own core version number as part of the client name, before the slash
			-- Therefore we don't include the slash in the match
        OR	CHARINDEX('WebMatrix', @value) > 0

			-- NuGet Package Explorer
		OR	CHARINDEX('NuGet Package Explorer Metro/', @value) > 0
        OR	CHARINDEX('NuGet Package Explorer/', @value) > 0
		)

        RETURN CAST(SUBSTRING(
                @value, 
                CHARINDEX('.', @value, CHARINDEX('/', @value) + 1) + 1, 
                (CHARINDEX('.', CONCAT(@value, '.'), CHARINDEX('.', @value, CHARINDEX('/', @value) + 1) + 1)) - ((CHARINDEX('.', @value, CHARINDEX('/', @value) + 1)) + 1)
            ) AS INT)

    RETURN 0
END
GO

IF OBJECT_ID('[dbo].[UserAgentClientCategory]') IS NOT NULL
    DROP FUNCTION [dbo].[UserAgentClientCategory]
GO

CREATE FUNCTION [dbo].[UserAgentClientCategory] (@value NVARCHAR(900))
RETURNS VARCHAR(64)
AS
BEGIN
    IF	(
			-- VS NuGet 2.8+ User Agent Strings
			CHARINDEX('NuGet VS PowerShell Console/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog - Solution/', @value) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog/', @value) > 0

			-- VS NuGet (pre-2.8) User Agent Strings
		OR	CHARINDEX('NuGet Add Package Dialog/', @value) > 0
        OR	CHARINDEX('NuGet Command Line/', @value) > 0
        OR	CHARINDEX('NuGet Package Manager Console/', @value) > 0
        OR	CHARINDEX('NuGet Visual Studio Extension/', @value) > 0
        OR	CHARINDEX('Package-Installer/', @value) > 0
		)
        RETURN 'NuGet'

		-- WebMatrix includes its own core version number as part of the client name, before the slash
		-- Therefore we don't include the slash in the match
    IF	CHARINDEX('WebMatrix', @value) > 0
        RETURN 'WebMatrix'

	IF	(
			-- NuGet Package Explorer
			CHARINDEX('NuGet Package Explorer Metro/', @value) > 0
		OR	CHARINDEX('NuGet Package Explorer/', @value) > 0
		)
		RETURN 'NuGet Package Explorer'

    IF (CHARINDEX('Mozilla', @value) > 0 or CHARINDEX('Opera', @value) > 0)
        RETURN 'Browser'

    RETURN ''
END
GO

IF OBJECT_ID('[dbo].[RefreshUserAgents]') IS NOT NULL
    DROP PROCEDURE [dbo].[RefreshUserAgents]
GO

CREATE PROCEDURE [dbo].[RefreshUserAgents]
AS
BEGIN

	SELECT		*
	FROM		(
				SELECT		Value
						,	Client AS OldClient
						,	[dbo].[UserAgentClient](Value) AS NewClient
						,	ClientMajorVersion AS OldClientMajorVersion
						,	ClientMinorVersion AS OldClientMinorVersion
						,	[dbo].[UserAgentClientMajorVersion](Value) AS NewClientMajorVersion
						,	[dbo].[UserAgentClientMinorVersion](Value) AS NewClientMinorVersion
						,	ClientCategory As OldClientCategory
						,	[dbo].[UserAgentClientCategory](Value) AS NewClientCategory
				FROM		Dimension_UserAgent
				) Map
	WHERE		Map.NewClient <> Map.OldClient
			OR	Map.NewClientMajorVersion <> Map.OldClientMajorVersion
			OR	Map.NewClientMinorVersion <> Map.OldClientMinorVersion
			OR	Map.NewClientCategory <> Map.OldClientCategory
	ORDER BY	OldClientCategory
			,	NewClientCategory
			,	OldClient
			,	NewClient
			,	OldClientMajorVersion
			,	OldClientMinorVersion
			,	NewClientMajorVersion
			,	NewClientMinorVersion

	UPDATE		Dimension_UserAgent
	SET			Client = [dbo].[UserAgentClient](Value)
			,	ClientMajorVersion = [dbo].[UserAgentClientMajorVersion](Value)
			,	ClientMinorVersion = [dbo].[UserAgentClientMinorVersion](Value)
			,	ClientCategory = [dbo].[UserAgentClientCategory](Value)
	WHERE		Client <> [dbo].[UserAgentClient](Value)
			OR	ClientMajorVersion <> [dbo].[UserAgentClientMajorVersion](Value)
			OR	ClientMinorVersion <> [dbo].[UserAgentClientMinorVersion](Value)
			OR	ClientCategory <> [dbo].[UserAgentClientCategory](Value)

END
GO
