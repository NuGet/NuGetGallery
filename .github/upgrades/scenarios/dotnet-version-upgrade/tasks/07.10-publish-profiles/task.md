# 07.10-publish-profiles: Create publish profiles for Admin/NoAdmin artifacts

## Objective
Create two publish profiles to produce Admin-enabled and Admin-disabled deployment artifacts.

## Scope
- Create `Properties/PublishProfiles/Admin.pubxml`:
  - Includes appsettings.json with AdminPanelEnabled=true
  - Standard ASP.NET Core publish settings
- Create `Properties/PublishProfiles/NoAdmin.pubxml`:
  - Includes appsettings.NoAdmin.json override
  - Sets AdminPanelEnabled=false
- Test both publish profiles:
  - `dotnet publish /p:PublishProfile=Admin /p:Configuration=Release`
  - `dotnet publish /p:PublishProfile=NoAdmin /p:Configuration=Release`
- Document new build commands for external pipeline
- Verify Admin routes registered/not registered based on config

## External Pipeline Commands (Documentation)
```powershell
# Admin-enabled artifact
dotnet publish $galleryProjectPath /p:PublishProfile=Admin /p:Configuration=Release /p:PublishDir=$publishDirAdmin

# Admin-disabled artifact
dotnet publish $galleryProjectPath /p:PublishProfile=NoAdmin /p:Configuration=Release /p:PublishDir=$publishDirNoAdmin
```

## Dependencies
- Blocked on: 07.09 (need full app working)

## Done When
- Two publish profiles created
- Both artifacts can be built via dotnet publish
- Admin functionality present/absent based on profile
- Build documentation updated
