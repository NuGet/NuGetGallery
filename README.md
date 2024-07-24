# Common libraries used across the NuGet server repositories

This repository contains shared libraries used across the NuGet server repositories, including:
* NuGet.Services.Configuration
* NuGet.Services.KeyVault
* NuGet.Services.Logging
* NuGet.Services.Owin
* NuGet.Services.Cursor
* NuGet.Services.Storage

## Getting started

First install prerequisites:

1. Visual Studio 2022 - Install the following [`Workloads`](https://docs.microsoft.com/visualstudio/install/modify-visual-studio) and individual components:
    * Azure development

The "Azure development" workload installs SQL Server Express LocalDB which is the database configured for local development.

Now run the NuGet ServerCommon:

1. Clone the repository with `git clone https://github.com/NuGet/ServerCommon.git`
2. Navigate to `.\servercommon`
3. Build with `.\build.ps1`
4. Create the database:
    Open Package Manager Console, set `NuGet.Services.Validation` as default project, then run `Update-Database`.
5. Open `.\NuGet.Server.Common.sln` using Visual Studio

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft’s Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.

## Reporting issues

Please report issues to the [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery/issues) repository, the home of all NuGet server-related issues.
