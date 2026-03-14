# Script to remove OWIN project after closing Visual Studio
# Run this after closing VS to complete the OWIN removal

Write-Host "Removing OWIN project and references..." -ForegroundColor Cyan

# 1. Remove OWIN project reference from NuGetGallery.csproj
Write-Host "Removing OWIN reference from NuGetGallery.csproj..."
$galleryProj = "src\NuGetGallery\NuGetGallery.csproj"
$content = Get-Content $galleryProj -Raw
$content = $content -replace '    <ProjectReference Include="\.\.\\NuGet\.Services\.Owin\\NuGet\.Services\.Owin\.csproj">\r?\n      <Project>\{56d69b64-c806-42fa-9aea-baa8d3ce97b4\}</Project>\r?\n      <Name>NuGet\.Services\.Owin</Name>\r?\n    </ProjectReference>\r?\n', ''
$content | Set-Content $galleryProj -NoNewline

# 2. Remove OWIN project from solution (if VS didn't do it already)
Write-Host "Removing OWIN projects from solution..."
try {
    dotnet sln NuGetGallery.sln remove src/NuGet.Services.Owin/NuGet.Services.Owin.csproj 2>$null
} catch {
    Write-Host "  (Already removed or not found)"
}

try {
    dotnet sln NuGetGallery.sln remove tests/NuGet.Services.Owin.Tests/NuGet.Services.Owin.Tests.csproj 2>$null
} catch {
    Write-Host "  (Test project not found)"
}

# 3. Delete OWIN project files
Write-Host "Deleting OWIN project files..."
if (Test-Path "src\NuGet.Services.Owin") {
    Remove-Item -Path "src\NuGet.Services.Owin" -Recurse -Force
    Write-Host "  Deleted src\NuGet.Services.Owin"
}

if (Test-Path "tests\NuGet.Services.Owin.Tests") {
    Remove-Item -Path "tests\NuGet.Services.Owin.Tests" -Recurse -Force
    Write-Host "  Deleted tests\NuGet.Services.Owin.Tests"
}

# 4. Update the upgrade execution log
Write-Host "Updating execution log..."
$logPath = ".github\upgrades\scenarios\dotnet-version-upgrade\execution-log.md"
$logContent = Get-Content $logPath -Raw
$newEntry = @"


## [$(Get-Date -Format 'yyyy-MM-dd HH:mm')] OWIN-removal

✅ **Removed NuGet.Services.Owin project**: OWIN is not needed for ASP.NET Core migration

**Rationale:**
- ASP.NET Core has its own native middleware pipeline (doesn't use OWIN)
- Microsoft.Owin is a legacy ASP.NET Framework package
- ForceSslMiddleware functionality will be replaced by ASP.NET Core's built-in `UseHttpsRedirection()` middleware
- Keeping OWIN would create dead-end code that gets deleted during ASP.NET Core migration anyway

**Changes:**
- Removed NuGet.Services.Owin project from solution
- Removed NuGet.Services.Owin.Tests project from solution
- Removed project reference from NuGetGallery.csproj
- Deleted OWIN project files

**Next Steps:**
When migrating NuGetGallery to ASP.NET Core (Task 07), replace OWIN startup with ASP.NET Core startup and use `app.UseHttpsRedirection()` for SSL enforcement.

"@

$logContent = $logContent + $newEntry
$logContent | Set-Content $logPath -NoNewline

Write-Host ""
Write-Host "✅ OWIN removal complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the changes with: git status"
Write-Host "2. Commit the changes with: git add -A; git commit -m 'Remove OWIN project - not needed for ASP.NET Core migration'"
Write-Host "3. Reopen Visual Studio"
Write-Host ""
