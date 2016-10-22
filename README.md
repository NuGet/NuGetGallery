[NuGet Gallery](http://nuget.org/) — Where packages are found 
=======================================================================

[![Build status](https://ci.appveyor.com/api/projects/status/6ob8lbutfecvi5n3/branch/master?svg=true)](https://ci.appveyor.com/project/NuGetteam/nugetgallery/branch/master)

This is an implementation of the NuGet Gallery and API. This serves as the back-end and community 
website for the NuGet client. For information about the NuGet project, visit the [Home repository](https://github.com/nuget/home).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Build and Run the Gallery in (arbitrary number) easy steps

1. Prerequisites. Install these if you don't already have them:
 1. Visual Studio 2015 - Custom install so that you may also install Microsoft SQL Server Data Tools. This will provide the LocalDB that Windows Azure SDK requires.
 2. PowerShell 2.0 (comes with Windows 7+)
 3. [NuGet](http://docs.nuget.org/docs/start-here/installing-nuget)
 4. [Windows Azure SDK](http://www.microsoft.com/windowsazure/sdk/)
2. Clone it!
    
    ```git clone git@github.com:NuGet/NuGetGallery.git```
3. Build it!
    
    ```
    cd NuGetGallery
    .\build
    ```
4. Set up the website in IIS Express!
 1. We highly recommend using IIS Express. Use the [Web Platform Installer](http://microsoft.com/web) to install it if you don't have it already (it comes with recent versions of VS and WebMatrix though). Make sure to at least once run IIS Express as an administrator.
 2. In an ADMIN powershell prompt, run the `.\tools\Enable-LocalTestMe.ps1` file. It allows non-admins to host websites at: `http(s)://nuget.localtest.me`, it configures an IIS Express site at that URL and creates a self-signed SSL certificate. For more information on `localtest.me`, check out [readme.localtest.me](http://readme.localtest.me). However, because [Server Name Indication](https://en.wikipedia.org/wiki/Server_Name_Indication) is not supported in the Network Shell on versions of Windows before 8, you must have at least Windows 8 to run this script successfully.
 3. If you're having trouble, go to the _Project Properties_ for the Website project, click on the _Web_ tab and change the URL to `localhost:port` where _port_ is some port number above 1024.
 
5. Create the Database!
  
  There are two ways you can create the databases. From Visual Studio 2015 or from the command line.
  
  1. From Visual Studio 2015
    1. Open Visual Studio 2015
    2. Open the Package Manager Console window
    3. Ensure that the Default Project is set to `NuGetGallery`
    4. Open the NuGetGallery.sln solution from the root of this repository. ***Important:*** Make sure the Package Manager Console has been opened once before you open the solution. If the solution was already open, open the package manager console and then close and re-open the solution (from the file menu)
    5. Run the following command in the Package Manager Console:
    
       ``` powershell
       Update-Database -StartUpProjectName NuGetGallery -ConfigurationTypeName MigrationsConfiguration
       ```
    If this fails, you are likely to get more useful output by passing `-Debug` than `-Verbose`.
  2. From the command line. ***Important:*** You must have successfully built the Gallery (step 3) for this to succeed.
    * Run `Update-Databases.ps1` in the `tools` folder to migrate the databases to the latest version.
      * To Update both databases, Nuget Gallery and Support Request, run this command
        ``` powershell
        .\tools\Update-Databases.ps1 -MigrationTargets NugetGallery,NugetGallerySupportRequest
        ```
      * To update only the Nuget Gallery DB, run this
        ``` powershell
        .\tools\Update-Databases.ps1 -MigrationTargets NugetGallery
        ```
      * And to update only the Support Request DB, run this
        ``` powershell
        .\tools\Update-Databases.ps1 -MigrationTargets NugetGallerySupportRequest
        ```
    * Additionally you can provide a `-NugetGallerySitePath` parameter to the `Update-Databases.ps1` script to indicate that you want to perform the migration on a site other than the one that is built with this repository.

6. When working with the gallery, e-mail messages are saved to the file system (under `~/App_Data`).
    * To change this to use an SMTP server, edit `src\NuGetGallery\Web.Config` and add a `Gallery.SmtpUri` setting. Its value should be an SMTP connection string, for example `smtp://user:password@smtpservername:25`.
    * To turn off e-mail confirmations, edit `src\NuGetGallery\Web.Config` and change the value of `Gallery.ConfirmEmailAddresses` to `false`.

7. Ensure the 'NuGetGallery' project (under the Frontend folder) is set to the Startup Project
  

That's it! You should now be able to press Ctrl-F5 to run the site!

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

1. Clone and checkout the following branches (to make sure local copies are made): '
2. '.

### When starting a new feature/unit of work.
    
1.  __Pull the latest.__
    Begin by pulling to make sure you are up-to-date before creating a branch to do your work 
    This assumes you have no local commits that haven't yet been pushed (i.e., that you were 
    previously up-to-date with origin).
    
        git checkout dev
        git pull dev
    
2.  __Create a topic branch to do your work.__
    You must work in topic branches, in order to help us keep our features isolated and easily moved between branches.
    Our policy is to start all topic branches off of the 'dev' branch. 
    Branch names should use the following format '[user]-[bugnumber]-[shortdescription]'. If there is no bug yet, 
    create one and assign it to yourself!

        git checkout dev
        git checkout -b anurse-123-makesuckless
    
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
    Start a code review by pushing your branch up to GitHub (```git push origin anurse-123-makesuckless```) and 
    creating a Pull Request from your branch to ***dev***. Wait for at least someone on the team to respond with: ":shipit:" (that's called the
    "Ship-It Squirrel" and you can put it in your own comments by typing ```:shipit:```).

5.  __Merge your changes in to dev.__
    Click the bright green "Merge" button on your pull request! Don't forget to delete the branch afterwards to keep our repo clean.

    If there isn't a bright green button... well, you'll have to do some more complicated merging:

        git checkout dev
        git pull origin dev
        git merge anurse-123-makesuckless
        ... resolve conflicts ...
        git push origin dev
    
6.  __Be ready to guide your change through QA, Staging and Prod__
    Your change will make its way through the QA, Staging and finally Prod branches as it's deployed to the various environments. Be prepared to fix additional bugs!
