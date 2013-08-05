

IF OBJECT_ID('[dbo].[UserAgentClient]') IS NOT NULL
    DROP FUNCTION [dbo].[UserAgentClient]
GO

--  'Nexus' refer to www.sonatype.com for details on NuGet integration
--  'JetBrains TeamCity' refer to www.jetbrains.com for details on NuGet integration
--  'Artifactory' refer to www.jfrog.com for details on NuGet integration

CREATE FUNCTION [dbo].[UserAgentClient] (@value nvarchar(900))
RETURNS NVARCHAR(128)
AS
BEGIN
    IF CHARINDEX('NuGet Add Package Dialog', @value) > 0 
        RETURN 'NuGet Add Package Dialog'
    IF CHARINDEX('NuGet Command Line', @value) > 0 
        RETURN 'NuGet Command Line'
    IF CHARINDEX('NuGet Package Explorer Metro', @value) > 0 
        RETURN 'NuGet Package Explorer Metro'
    IF CHARINDEX('NuGet Package Explorer', @value) > 0 
        RETURN 'NuGet Package Explorer'
    IF CHARINDEX('NuGet Package Manager Console', @value) > 0 
        RETURN 'NuGet Package Manager Console'
    IF CHARINDEX('NuGet Visual Studio Extension', @value) > 0 
        RETURN 'NuGet Visual Studio Extension'
    IF CHARINDEX('WebMatrix', @value) > 0 
        RETURN 'WebMatrix'
    IF CHARINDEX('Package-Installer', @value) > 0 
        RETURN 'Package-Installer'
    IF CHARINDEX('JetBrains TeamCity', @value) > 0 
        RETURN 'JetBrains TeamCity'
    IF CHARINDEX('Nexus', @value) > 0 
        RETURN 'Sonatype Nexus'
    IF CHARINDEX('Artifactory', @value) > 0 
        RETURN 'JFrog Artifactory'
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
    IF (CHARINDEX('NuGet Add Package Dialog/', @value) > 0
        OR CHARINDEX('NuGet Command Line/', @value) > 0
        OR CHARINDEX('NuGet Package Explorer/', @value) > 0
        OR CHARINDEX('NuGet Package Manager Console/', @value) > 0
        OR CHARINDEX('NuGet Visual Studio Extension/', @value) > 0
        OR CHARINDEX('WebMatrix', @value) > 0
        OR CHARINDEX('Package-Installer/', @value) > 0)
        
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
    IF (CHARINDEX('NuGet Add Package Dialog/', @value) > 0
        OR CHARINDEX('NuGet Command Line/', @value) > 0
        OR CHARINDEX('NuGet Package Explorer/', @value) > 0
        OR CHARINDEX('NuGet Package Manager Console/', @value) > 0
        OR CHARINDEX('NuGet Visual Studio Extension/', @value) > 0
        OR CHARINDEX('WebMatrix', @value) > 0
        OR CHARINDEX('Package-Installer/', @value) > 0)

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
    IF (CHARINDEX('NuGet Add Package Dialog', @value) > 0
        OR CHARINDEX('NuGet Command Line', @value) > 0
        OR CHARINDEX('NuGet Package Explorer Metro', @value) > 0
        OR CHARINDEX('NuGet Package Explorer', @value) > 0
        OR CHARINDEX('NuGet Package Manager Console', @value) > 0
        OR CHARINDEX('NuGet Visual Studio Extension', @value) > 0
        OR CHARINDEX('Package-Installer', @value) > 0)
        RETURN 'NuGet'

    IF CHARINDEX('WebMatrix', @value) > 0
        RETURN 'WebMatrix'

    IF (CHARINDEX('Mozilla', @value) > 0 or CHARINDEX('Opera', @value) > 0)
        RETURN 'Browser'

    RETURN ''
END
GO

