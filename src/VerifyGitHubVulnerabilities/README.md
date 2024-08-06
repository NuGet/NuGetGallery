## Overview

This is a recurring, monitoring job for taking a current snapshot of the whole of GitHub's security advisory collection (pertaining to the `NUGET` ecosystem) and comparing it with the vulnerability records in the gallery database. It's designed to interact with GitHub, the NuGet Gallery DB, and V3 metadata endpoints in a read-only manner. It just reports problems when it finds them.

## Running the job locally

A typical command line will look like this:

```
VerifyGitHubVulnerabilities.exe -Configuration appsettings.json -InstrumentationKey <key> -HeartbeatIntervalSeconds 60
```

Setup for this command:

1. Install the certificate used to authenticate as our client Microsoft Entra ID app registration into your `CurrentUser` certificate store.
1. Create a file called `appsettings.json` in the same driectory as the `VerifyGitHubVulnerabilities.exe`. The contents of this JSON file should look like the following:

   ```
   {
     "GalleryDb": {
       "ConnectionString": <connection string>
     },
     "Initialization": {
       "GitHubPersonalAccessToken": "<PAT for GitHub database access>",
       "NuGetV3Index": "<index for v3 endpoint>"
     },
     "KeyVault_VaultName": "<key vault for secrets>",
     "KeyVault_UseManagedIdentity": true
   }
   ```


## Algorithm

This service runs by:
1. Egressing all current security advisories pertaining to the `NUGET` ecosystem from GitHub using the same GraphQL query used in `GitHubVulnerabilities2Db`.
1. Running `GitHubVulnerabilities2Db` update logic, substituing a verification visitor for the code that ordinarily does the updating
1. Checking the presence and accuracy of advisories in the provided SQL server database, reporting missing advisories, incorrect severities and advisory URLs, and incorrect/missing package ranges.