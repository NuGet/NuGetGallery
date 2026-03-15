# 07.07-authentication: Migrate authentication to ASP.NET Core

## Objective
Migrate OWIN-based authentication to ASP.NET Core Authentication.

## Scope
- Migrate cookie authentication (OWIN → ASP.NET Core cookies)
- Migrate external auth providers:
  - Microsoft Account (OWIN → ASP.NET Core Microsoft provider)
  - Azure AD v2 (OWIN OpenID Connect → ASP.NET Core OpenID Connect)
- Update authentication configuration to use appsettings.json
- Migrate claims transformation logic
- Update [Authorize] attributes if needed

## Key Migrations
- app.UseCookieAuthentication(new CookieAuthenticationOptions {...}) → services.AddAuthentication().AddCookie()
- app.UseOpenIdConnectAuthentication() → services.AddAuthentication().AddOpenIdConnect()
- OwinContext.Authentication → HttpContext.SignInAsync/SignOutAsync

## Dependencies
- Blocked on: 07.06 (need middleware pipeline)

## Research Findings

### Current OWIN Authentication Setup (from OwinStartup.cs)
1. **LocalUserAuthenticator** (Cookie-based, lines 122-127)
   - Active authentication mode
   - Login path: /users/account/LogOn
   - 6-hour expiration with sliding
   - CookieSecure based on RequireSSL config

2. **External Sign-In Cookie** (lines 130-137)
   - Passive mode for external auth
   - 5-minute expiration
   - Used as intermediate cookie during external auth flow

3. **Microsoft Account** (MicrosoftAccountAuthenticator)
   - OWIN middleware: Microsoft.Owin.Security.MicrosoftAccount
   - Scopes: wl.emails, wl.signin

4. **Azure AD v2** (AzureActiveDirectoryV2Authenticator)
   - OWIN middleware: Microsoft.Owin.Security.OpenIdConnect
   - Supports both personal MSA and organizational accounts
   - Multi-factor authentication support (ACR claims)
   - Callback: users/account/authenticate/return

5. **ApiKey Authentication** (ApiKeyAuthenticator)
   - Custom OWIN middleware for API key auth
   - Used for NuGet package push/pull operations

### Authentication Architecture
- **Authenticator base class** provides Startup() method that calls AttachToOwinApp()
- **AuthenticationService** manages collection of authenticators
- **AuthDependenciesModule** (Autofac) registers all authenticators
- Authentication types defined in AuthenticationTypes class

### Migration Strategy
1. **Keep authenticator abstraction** - Modify Authenticator base class to support both OWIN and ASP.NET Core
2. **Add ASP.NET Core auth in Program.cs** - Configure services.AddAuthentication() with cookie + external providers
3. **Migrate authenticators** - Update each authenticator to configure ASP.NET Core auth instead of OWIN
4. **Update middleware ordering** - Place UseAuthentication()/UseAuthorization() in correct position (already placeholders in Program.cs)
5. **Controllers will need updates in 07.08** - HttpContext.GetOwinContext().Authentication → HttpContext (built-in)

### ASP.NET Core Authentication Setup
- services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
- .AddCookie() for local user authentication
- .AddCookie() for external sign-in (intermediate)
- .AddMicrosoftAccount() for Microsoft Account
- .AddOpenIdConnect() for Azure AD v2
- ApiKey authentication will need custom authentication handler (already has ApiKeyAuthenticationHandler)

## Done When
- Cookie authentication works
- External auth providers configured
- Login/logout functional
- Authorization attributes work
