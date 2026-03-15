# 07.06-middleware-migration: Migrate OWIN middleware to ASP.NET Core middleware

## Objective
Replace OWIN middleware pipeline with ASP.NET Core middleware equivalents.

## Scope
- Load migrating-owin-to-middleware skill for guidance
- Identify all OWIN middleware in current Startup.cs
- Replace OWIN middleware with ASP.NET Core equivalents:
  - app.UseErrorPage() → app.UseDeveloperExceptionPage()
  - app.UseCookieAuthentication() → app.UseAuthentication()
  - app.UseStatic() → app.UseStaticFiles()
  - Custom OWIN middleware → custom ASP.NET Core middleware
- Configure middleware pipeline order correctly
- Remove Microsoft.Owin.* package references

## Dependencies
- Blocked on: 07.05 (need Program.cs)

## Done When
- All OWIN middleware converted
- Middleware pipeline configured in Program.cs
- No OWIN package references remain
- Pipeline order correct
