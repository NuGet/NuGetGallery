CREATE FUNCTION [dbo].[IsNuGetClient]
(
	@ClientName NVARCHAR(128)
)
RETURNS BIT
WITH SCHEMABINDING
AS
BEGIN
	IF @ClientName IS NULL
		RETURN 0

	IF	(
			-- VS NuGet 2.8+
			CHARINDEX('NuGet VS PowerShell Console', @ClientName) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog - Solution', @ClientName) > 0
		OR	CHARINDEX('NuGet VS Packages Dialog', @ClientName) > 0

			-- VS NuGet (pre-2.8)
		OR	CHARINDEX('NuGet Add Package Dialog', @ClientName) > 0
        OR	CHARINDEX('NuGet Command Line', @ClientName) > 0
        OR	CHARINDEX('NuGet Package Manager Console', @ClientName) > 0
        OR	CHARINDEX('NuGet Visual Studio Extension', @ClientName) > 0
        OR	CHARINDEX('Package-Installer', @ClientName) > 0
		)
		RETURN 1

	RETURN 0
END