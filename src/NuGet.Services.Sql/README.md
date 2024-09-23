# NuGet.Services.Sql

A library that provides support for using Entra ID apps for generating tokens for authenticating SQL connections.
Authenticating to Entra ID app is done using certificates, which are automatically refreshed from key vault.
The tokens provided by Entra ID App are cached and reused as long as `AzureSqlConnectionFactory` instance is kept
alive and reused for creating SQL connections.

## Caveats

When used in ASP.NET/ASP.NET Core app in an Azure app service attempts to retrieve a token from Entra ID app might
fail with

> Failed to acquire access token for <DB name>.

With inner exception message:

> The system cannot find the file specified.

thrown by `X509Certificate2` constructor.

This can be worked around by [setting](https://stackoverflow.com/a/62790919) `WEBSITE_LOAD_USER_PROFILE=1` variable for the app service.
