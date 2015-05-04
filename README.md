[NuGet Gallery](http://nuget.org/) — Where packages are found 
=======================================================================
This is an implementation of the NuGet Gallery and API. This serves as the back-end and community 
website for the NuGet client. For information about the NuGet clients, visit http://nuget.codeplex.com/

## Build and Run the Gallery in (arbitrary number) easy steps

1. Prerequisites. Install these if you don't already have them:
 1. Visual Studio 2013
 2. PowerShell 2.0 (comes with Windows 7+)
 3. [NuGet](http://docs.nuget.org/docs/start-here/installing-nuget)
 4. [Windows Azure SDK](http://www.microsoft.com/windowsazure/sdk/) - Note that you may have to manually upgrade the ".Cloud" projects in the solution if a different SDK version is used.
 5. (Optional, for unit tests) [xUnit for Visual Studio 2012 and 2013](http://visualstudiogallery.msdn.microsoft.com/463c5987-f82b-46c8-a97e-b1cde42b9099)
2. Clone it!
    
    ```git clone git@github.com:NuGet/NuGetGallery.git```
3. Build it!
    
    ```
    cd NuGetGallery
    .\build
    ```
4. Set up the website in IIS Express!
 1. We highly recommend using IIS Express. Use the [Web Platform Installer](http://microsoft.com/web) to install it if you don't have it already (it comes with recent versions of VS and WebMatrix though)
 2. In an ADMIN powershell prompt, run the `.\tools\Enable-LocalTestMe.ps1` file. It allows non-admins to host websites at: `http(s)://nuget.localtest.me`, it configures an IIS Express site at that URL and creates a self-signed SSL certificate. For more information on `localtest.me`, check out [readme.localtest.me](http://readme.localtest.me).
 3. If you're having trouble, go to the _Project Properties_ for the Website project, click on the _Web_ tab and change the URL to `localhost:port` where _port_ is some port number above 1024.
 4. When running the application using the Azure Compute emulator, you may have to edit the `.\src\NuGetGallery.Cloud\ServiceConfiguration.Local.cscfg` file and set the certiciate thumbrint for the setting `SSLCertificate` to the certificate thumbprint of the generated `nuget.localtest.me` certificate from step 2. You can get a list of certificates and their thumbprints using PowerShell, running `Get-ChildItem -path cert:\LocalMachine\My`.

5. Create the Database!
 1. Open Visual Studio 2013
 2. Open the Package Manager Console window
 3. Ensure that the Default Project is set to `NuGetGallery`
 4. Open the NuGetGallery.sln solution from the root of this repository. ***Important:*** Make sure the Package Manager Console has been opened once before you open the solution. If the solution was already open, open the package manager console and then close and re-open the solution (from the file menu)
 5. Run the following command in the Package Manager Console:
 
    ```
    Update-Database
    ```
If this fails, you are likely to get more useful output by passing -Debug than -Verbose.

6. Change the value of Gallery.ConfirmEmailAddresses to false in Web.Config file under src\NuGetGallery, this is required to upload the packages after registration.

7. Ensure the 'NuGetGallery' project (under the Frontend folder) is set to the Startup Project
  

That's it! You should now be able to press Ctrl-F5 to run the site!

## Contribute
If you find a bug with the gallery, please visit the Issue tracker (https://github.com/NuGet/NuGetGallery/issues) and 
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
communicating.

To contribute to the gallery, make sure to create a fork first. Make your changes in the fork following 
the Git Workflow. When you are done with your changes, send us a pull request.

## Copyright and License
Copyright 2015 .NET Foundation

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this work except in compliance with 
the License. You may obtain a copy of the License in the LICENSE file, or at:

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on 
an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the 
specific language governing permissions and limitations under the License.

## The Git Workflow

This is the Git workflow we're currently using:

### Setting up

1. Clone and checkout the following branches (to make sure local copies are made): 'master', 'iter-start'

### When starting a new feature/unit of work.
    
1.  __Pull the latest.__
    Begin by pulling to make sure you are up-to-date before creating a branch to do your work 
    This assumes you have no local commits that haven't yet been pushed (i.e., that you were 
    previously up-to-date with origin).
    
        git checkout iter-start
        git pull iter-start
    
2.  __Create a topic branch to do your work.__
    You must work in topic branches, in order to help us keep our features isolated and easily moved between branches.
    Our policy is to start all topic branches off of the 'iter-start' branch. 
    Branch names should use the following format '[user]-[bugnumber]-[shortdescription]'. If there is no bug yet, 
    create one and assign it to yourself!

        git checkout iter-start
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
    creating a Pull Request from your branch to ***master***. Wait for at least someone on the team to respond with: ":shipit:" (that's called the
    "Ship-It Squirrel" and you can put it in your own comments by typing ```:shipit:```).

5.  __Merge your changes in to master.__
    Click the bright green "Merge" button on your pull request! **NOTE: DO NOT DELETE THE TOPIC BRANCH!!**

    If there isn't a bright green button... well, you'll have to do some more complicated merging:

        git checkout master
        git pull origin master
        git merge anurse-123-makesuckless
        ... resolve conflicts ...
        git push origin master
    
6.  __Be ready to guide your change through QA, Staging and Prod__
    Your change will make its way through the QA, Staging and finally Prod branches as it's deployed to the various environments. Be prepared to fix additional bugs!

**NOTE: DO NOT DELETE THE TOPIC BRANCH!!**
