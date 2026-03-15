# 07.05-program-startup: Create Program.cs and migrate Startup.cs from OWIN

## Objective
Create ASP.NET Core Program.cs and migrate application startup from OWIN-based Startup.cs.

## Scope
- Create new Program.cs with WebApplication.CreateBuilder
- Load appsettings.json configuration
- Configure services (DI container) - migrate from Autofac OWIN setup
- Configure Entity Framework 6 for .NET Core (if initialization needed)
- Set up logging (migrate from current logging to ASP.NET Core ILogger)
- Configure Kestrel web server
- Do NOT migrate middleware pipeline yet (that's 07.06)

## Key Changes
- OWIN Startup.Configuration(IAppBuilder app) → Program.cs WebApplicationBuilder
- Autofac container setup → services.AddAutofac() in Program.cs
- Application_Start logic → Program.cs initialization

## Dependencies
- Blocked on: 07.04 (need appsettings.json)

## Done When
- Program.cs created
- Service registration migrated
- EF6 configured for .NET Core
- App initializes (won't serve requests yet - needs middleware)
