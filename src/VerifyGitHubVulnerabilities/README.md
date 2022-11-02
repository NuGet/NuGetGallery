## Overview

This is a recurring, monitoring job for taking a current snapshot of the whole of GitHub's security advisory collection (pertaining to the `NUGET` ecosystem) and comparing it with the vulnerability records in the gallery database. It's designed to interact with GitHub, the NuGet Gallery DB, and V3 metadata endpoints in a read-only manner. It just reports problems when it finds them.

## Running the job locally

A typical command line will look like this:

```
VerifyGitHubVulnerabilities.exe -Configuration appsettings.json -InstrumentationKey <key> -HeartbeatIntervalSeconds 60
```

### Using DEV resources

The easiest way to run the tool if you are on the nuget.org team is to use the DEV environment resources:

1. Install the certificate used to authenticate as our client AAD app registration into your `CurrentUser` certificate store.
1. Clone our internal [`NuGetDeployment`](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeploymentp) repository.
1. Take a copy of the [DEV VerifyGitHubVulnerabilities appsettings.json](https://nuget.visualstudio.com/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%VerifyGitHubVulnerabilities%2FDEV%2Fnorthcentralus%2Fappsettings.json) file and place it in the same directory as the `VerifyGitHubVulnerabilities.exe`. This will use our secrets to authenticate to the SQL server (this file also contains a reference to the secret used for the access token to GitHub).
1. Run as per above.

## Algorithm

This service runs by:
1. Egressing all current security advisories pertaining to the `NUGET` ecosystem from GitHub using the same GraphQL query used in `GitHubVulnerabilities2Db`.
1. Running `GitHubVulnerabilities2Db` update logic, substituing a verification visitor for the code that ordinarily does the updating
1. Checking the presence and accuracy of advisories in the provided SQL server database, reporting missing advisories, incorrect severities and advisory URLs, and incorrect/missing package ranges.