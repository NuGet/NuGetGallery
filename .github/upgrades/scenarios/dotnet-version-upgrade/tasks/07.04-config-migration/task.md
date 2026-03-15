# 07.04-config-migration: Migrate Web.config to appsettings.json and Program.cs

## Objective
Migrate configuration from Web.config to ASP.NET Core configuration system (appsettings.json, appsettings.{Environment}.json, Program.cs).

## Scope
- Create appsettings.json with all Gallery.* settings from Web.config
- Create appsettings.Development.json, appsettings.Production.json for environment-specific settings
- Migrate appSettings section → appsettings.json
- Migrate connectionStrings section → appsettings.json (or user secrets/env vars)
- Document transformation strategy (Web.Debug.config → appsettings.Development.json)
- Preserve Gallery.AdminPanelEnabled flag for conditional Admin area
- Keep Web.config for any remaining IIS-specific settings (if needed)

## Admin Area Configuration
- Ensure AdminPanelEnabled setting is in appsettings.json
- Document how build produces two artifacts (MSBuild condition, publish profiles, etc.)
- Create separate publish profiles or configuration transforms

## Done When
- appsettings.json created with all settings
- Environment-specific configs created
- Admin panel configuration strategy documented
- Web.config backed up (may still be referenced for documentation)
