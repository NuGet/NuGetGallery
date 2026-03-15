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

## Done When
- Cookie authentication works
- External auth providers configured
- Login/logout functional
- Authorization attributes work
