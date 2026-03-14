# 08-dependent-applications: Upgrade Tier 7-9 Applications and Tools

Upgrade remaining 16 projects spanning Tiers 7-9: database migration tools, validation jobs, GitHub vulnerability tools, account management, verification tools, and all test projects.

These are top-level applications and tools that depend on the upgraded libraries and web app.

**Projects**:
- Tier 7: DatabaseMigrationTools, Validation.Common.Job, VerifyMicrosoftPackage
- Tier 8: AccountDeleter, GitHubVulnerabilities2Db, GitHubVulnerabilities2V3, GalleryTools, NuGetGallery.Facts, VerifyGitHubVulnerabilities
- Tier 9: AccountDeleter.Facts, GitHubVulnerabilities2Db.Facts, GitHubVulnerabilities2v3.Facts, NuGet.Services.DatabaseMigration.Facts, VerifyMicrosoftPackage.Facts, GalleryTools (others at this level)

**Key concerns**:
- NuGetGallery.Facts: 1040+ API issues (largest test project, depends on web app)
- Deprecated packages in multiple tools (Microsoft.Extensions.CommandLineUtils)
- Console application hosts may need host builder updates

**Done when**:
- All 16 projects target net10.0
- All packages updated
- Projects build without errors
- All tests pass

