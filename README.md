﻿[NuGet Gallery](https://www.nuget.org/) — Where packages are found 
=======================================================================

This  project powers [nuget.org](https://www.nuget.org), the home for .NET's open-source ecosystem. For information about NuGet, visit the [Home repository](https://github.com/nuget/home).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Getting started

First install prerequisites:

1. Visual Studio 2019 - Install the following [`Workloads`](https://docs.microsoft.com/visualstudio/install/modify-visual-studio):
    * ASP.NET and web development
    * Azure development
2. PowerShell 4.0
3. SQL Server 2016 (with DB engine version 13.0 or greater)

Now run the NuGet Gallery:

1. Clone the repository with `git clone https://github.com/NuGet/NuGetGallery.git`
2. Navigate to `.\NuGetGallery`
3. Build with `.\build.ps1`
4. Create the database and enable HTTPS with `.\tools\Setup-DevEnvironment.ps1`
5. Open `.\NuGetGallery.sln` using Visual Studio
6. Ensure the `NuGetGallery` project is the StartUp Project and [press `F5` to run the site](https://docs.microsoft.com/visualstudio/get-started/csharp/run-program)

Refer to [our documentation](./docs/) for information on how to develop the frontend, use AAD, and more.

## Deploy

You will find instructions on how to deploy the Gallery to Azure [here](https://github.com/NuGet/NuGetGallery/blob/master/docs/Deploying/README.md).

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
communicating. We have a list of items that are [up for grabs](https://github.com/NuGet/NuGetGallery/issues?q=is%3Aopen+is%3Aissue+label%3A%22Up+for+Grabs%22) and you can start working on (but always ping us beforehand).

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
        git checkout -b anurse-123
    
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
    Start a code review by pushing your branch up to GitHub (```git push origin anurse-123```) and
    creating a Pull Request from your branch to ***dev***. Wait for at least someone on the team to respond with: ":shipit:" (that's called the
    "Ship-It Squirrel" and you can put it in your own comments by typing ```:shipit:```).

5.  __Merge your changes in to dev.__
    Click the bright green "Merge" button on your pull request! Don't forget to delete the branch afterwards to keep our repo clean.

    If there isn't a bright green button... well, you'll have to do some more complicated merging:

        git checkout dev
        git pull origin dev
        git merge anurse-123
        ... resolve conflicts ...
        git push origin dev
    
6.  __Be ready to guide your change through QA, Staging and Prod__
    Your change will make its way through the QA, Staging and finally Prod branches as it's deployed to the various environments. Be prepared to fix additional bugs!

