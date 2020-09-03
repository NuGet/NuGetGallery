## Overview

This is a run-once job for taking a current snapshot of the whole of GitHub's security advisory collection (pertaining to the `NUGET` ecosystem) and comparing it with the vulnerability records in the gallery database. It's designed to be run on an ad hoc basis (not an ongoing monitoring job), from the command line, with all status message streamed to `stdout` and all vulnerability differences streamed to `stderr`. 

## Running the job

A typical command line will look like this:

```
verifygithubvulnerabilities.exe -Configuration appsettings.json -InstrumentationKey <key> -HeartbeatIntervalSeconds 60 2>c:\errors.txt
```

- the `2>` will direct `stderr` to a text file (a `1>` would similarly direct `stdout` but it's probably useful to have that print to console)
- the contents of the `appsettings.json` file will be similar (identical is fine--there are just some extra unneeded settings in them) to the settings files used for `GitHubVulnerabilities2Db`.

### Using DEV resources

The easiest way to run the tool if you are on the nuget.org team is to use the DEV environment resources:

1. Install the certificate used to authenticate as our client AAD app registration into your `CurrentUser` certificate store.
1. Clone our internal [`NuGetDeployment`](https://nuget.visualstudio.com/DefaultCollection/NuGetMicrosoft/_git/NuGetDeploymentp) repository.
1. Take a copy of the [DEV GitHubVulnerabilities2Db appsettings.json](https://nuget.visualstudio.com/NuGetMicrosoft/_git/NuGetDeployment?path=%2Fsrc%2FJobs%2FNuGet.Jobs.Cloud%2FJobs%2FGitHubVulnerabilities2Db%2FDEV%2Fnorthcentralus%2Fappsettings.json) file and place it in the same directory as the `verifygithubvulnerabilities.exe`. This will use our secrets to authenticate to the SQL server (this file also contains a reference to the secret used for the access token to GitHub).
1. Run as per above.

## Algorithm

This service runs by:
1. Egressing all current security advisories pertaining to the `NUGET` ecosystem from GitHub using the same GraphQL query used in `GitHubVulnerabilities2Db`.
1. Running `GitHubVulnerabilities2Db` update logic, substituing a verification visitor for the code that ordinarily does the updating
1. Checking the presence and accuracy of advisories in the provided SQL server database, reporting missing advisories, incorrect severities and advisory URLs, and incorrect/missing package ranges.