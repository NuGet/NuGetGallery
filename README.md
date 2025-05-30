﻿[NuGet Gallery](https://www.nuget.org/) — Where packages are found 
=======================================================================

This project powers [nuget.org](https://www.nuget.org), the home for .NET's open-source ecosystem. For information about NuGet, visit the [Home repository](https://github.com/nuget/home).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Getting started

First install prerequisites:

1. Visual Studio 2022 - Install the following [`Workloads`](https://docs.microsoft.com/visualstudio/install/modify-visual-studio) and individual components:
    * ASP.NET and web development
    * Azure development
    * Just web UI functional tests: "Web performance and load testing tools" individual component

Visual Studio 2019 may work but Visual Studio 2022 is recommended.

The "Azure development" workload installs SQL Server Express LocalDB which is the database configured for local development.

### Run the gallery website locally

This repository contains both the gallery web app (what runs www.nuget.org) as well as background jobs and shared libraries.

Let's focus on running the gallery web app locally, first.

1. Clone the repository with `git clone https://github.com/NuGet/NuGetGallery.git`
2. Navigate to `.\NuGetGallery`
3. Build with `.\build.ps1`
4. Create the database and enable HTTPS with `.\tools\Setup-DevEnvironment.ps1`
5. Open `.\NuGetGallery.sln` using Visual Studio
6. Ensure the `NuGetGallery` project is the StartUp Project and [press `F5` to run the site](https://docs.microsoft.com/visualstudio/get-started/csharp/run-program)

Refer to [our documentation](./docs/) for information on how to develop the frontend, use AAD, and more.

### Shared libraries

There are a set of shared libraries used across the NuGet server repositories, including:

* NuGet.Services.Configuration
* NuGet.Services.KeyVault
* NuGet.Services.Logging
* NuGet.Services.Owin
* NuGet.Services.Cursor
* NuGet.Services.Storage

To edit them, follow these steps:

1. Build with `.\build.ps1`
2. Open `.\NuGet.Server.Common.sln` using Visual Studio
3. Create the validation database:
   - Open Package Manager Console, set `NuGet.Services.Validation` as default project, then run `Update-Database`.

Note that many of these shared projects are also referenced by the other solution (e.g. in `NuGetGallery.sln`) files in the root of the repository, to simplify modification.

### Background jobs

This repository also contains nuget.org's implementation of the [NuGet V3 API](https://docs.microsoft.com/en-us/nuget/api/overview)
as well as many other back-end jobs for the operation of nuget.org.

1. Open `.\NuGet.Jobs.sln` using Visual Studio
2. Each job would be an exe with 2 main classes `Program` and `Job`
3. `Program.Main` should simply do the following and nothing more

    ```
    var job = new Job();
    JobRunner.Run(job, args).Wait();
    ```
    
4. Job class must inherit `JsonConfigurationJob`. This job based provides some dependency injection setup and has you set configuration in a JSON file.

Most jobs can be run locally with a `-Configuration {path_to_json}` command line argument. Not all follow this pattern.
Check the implementation in `Program.cs` or the `README.md` next to the `.csproj` project file for job-specific information.

## Deploy
### Deploy to Azure

You will find instructions on how to deploy the Gallery to Azure [here](docs/Deploying/README.md).

### Deploy locally
After you succeed in running the NuGet Gallery, you can create a publish profile to deploy locally (such as your local Windows computer). 

The steps are:
1. Select the `NuGetGallery` project in Solution Explore of Visual Studio. 
2. Right click the project, and then click `Publish` in the pop-up menu. Create a publish profile and make sure the Target is set to `Folder`.
3. Copy the contents of the `Target Location` to any folder you want. For the following example, assume the folder is `C:\ContosoSoftware\NuGetGallery`.
4. Execute the command below to start the web app (note that the parameter `/path` of iisexpress.exe only supports absolute paths on Windows).
    ```cmd
    "C:\Program Files\IIS Express\iisexpress.exe" /path:C:\ContosoSoftware\NuGetGallery
    ```

Now you can access the local website with a web browser. The URL is `https://localhost`.

After you deploy it, you don't need using Visual Studio to run it anymore.

## Contribute

If you find a bug with the gallery, please visit the [Issue tracker](https://github.com/NuGet/NuGetGallery/issues) and 
create an issue. If you're feeling generous, please search to see if the issue is already logged before creating a 
new one.

When creating an issue, clearly explain
* What you were trying to do.
* What you expected to happen.
* What actually happened.
* Steps to reproduce the problem.

Also include any information you think is relevant to reproducing the problem such as the browser version you used. 
Does it happen when you switch browsers. And so on.

## Submit a patch
Before starting work on an issue, either create an issue or comment on an existing issue to ensure that we're all 
communicating. We have a list of items that are [good first issue](https://github.com/NuGet/NuGetGallery/labels/good%20first%20issue) and you can start working on (but always ping us beforehand).

To contribute to the gallery, make sure to create a fork first. Make your changes in the fork following 
the Git Workflow. When you are done with your changes, send us a pull request.

## Copyright and License
Copyright .NET Foundation

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this work except in compliance with 
the License. You may obtain a copy of the License in the LICENSE file, or at:

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on 
an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the 
specific language governing permissions and limitations under the License.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow Microsoft’s Trademark & Brand Guidelines. Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.

## The Git Workflow

This is the Git workflow we're currently using:

### Setting up

Clone and checkout the `dev` branch.

Visual Studio may modify the `applicationhost.config` file. You can force git to ignore changes to this file
with:

    git update-index --assume-unchanged .vs/config/applicationhost.config

You can undo this with this command:

    git update-index --no-assume-unchanged .vs/config/applicationhost.config

This should help prevent unwanted file commits.

### When starting a new feature/unit of work.
    
1.  __Pull the latest.__
    Begin by pulling to make sure you are up-to-date before creating a branch to do your work 
    This assumes you have no local commits that haven't yet been pushed (i.e., that you were 
    previously up-to-date with origin).
    
        git checkout dev
        git pull dev
    
2.  __Create a topic branch to do your work.__
    You must work in topic branches to help us keep our features isolated and easily moved between branches.
    Our policy is to start all topic branches off of the 'dev' branch. 
    Branch names should use the following format '[user]-[bugnumber]'. If there is no bug yet,
    create one and assign it to yourself!

        git checkout dev
        git checkout -b billg-123
    
3.  __Do your work.__
    Now, do your work using the following highly accurate and efficient algorithm :)

    1. Make changes.
    2. Test your changes (you're practicing TDD, right?)
    3. Add your changes to git's index.
        
            git add -A

    4. Commit your changes.
        
            git commit -m "<description of work>"
        
    5. if (moreWorkToDo) go to #3.1 else go to #4.

4.  __Start a code review.__
    Start a code review by pushing your branch up to GitHub (```git push origin billg-123```) and
    creating a Pull Request from your branch to ***dev***. Wait for at least someone on the team to approve the PR.

5.  __Merge your changes in to dev.__
    Click the bright green "Merge" button on your pull request! Don't forget to delete the branch afterwards to keep our repo clean.

    If there isn't a bright green button... well, you'll have to do some more complicated merging:

        git checkout dev
        git pull origin dev
        git merge billg-123
        ... resolve conflicts ...
        git push origin dev
    
6.  __Be ready to guide your change through our deployed environments.__
    Your change will make its way through the DEV (dev.nugettest.org), INT (int.nugettest.org) and finally PROD (www.nuget.org). Be prepared to fix additional bugs!
