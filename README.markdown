[NuGet Gallery](http://nuget.org/) - Where packages are found
=======================================================================
This is an implementation of the NuGet Gallery and OData Package Feed. This serves as the back-end and community 
website for the NuGet client. For information about the NuGet clients, visit http://nuget.codeplex.com/

## Getting Started

To compile the project, you'll need Visual Studio 2010 or later and PowerShell 2.0. You'll also need to 
[install NuGet](http://docs.nuget.org/docs/start-here/installing-nuget). Also, make sure to install the 
[Windows Azure SDK](http://www.microsoft.com/windowsazure/sdk/).

To build the project, clone it locally:

    git clone git@github.com:NuGet/NuGetGallery.git
    cd NuGetGallery
    .\Build-Solution.ps1

The `Build-Solution.ps1` script will build the solution, run the facts (unit tests), and update the database (from migrations).

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
Copyright 2011 Outercurve Foundation

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this work except in compliance with 
the License. You may obtain a copy of the License in the LICENSE file, or at:

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on 
an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the 
specific language governing permissions and limitations under the License.

## The Git Workflow

This is the Git workflow we're currently using:

### When starting a new feature/unit of work.
    
1.  __Pull the latest.__
    Begin by pulling to make sure you are up-to-date before creating a branch to do your work 
    This assumes you have no local commits that haven't yet been pushed (i.e., that you were 
    previously up-to-date with origin).
    
        git pull 
    
2.  __Create a topic branch to do your work.__
    Working in a topic branch isolates your work and makes it easy to bring in updates from
    other developers without cluttering the history with merge commits. You should generally
    be working in topic branches. If you do make changes in the master branch, just run this
    command before commiting them, creating the branch just in time.

        git checkout -b <topic branch>
    
3.  __Do your work.__
    Follow this loop while making small changes and committing often.    

    1.  Make changes.
    2. Test your changes (you're practicing TDD, right?)
    3. Add your changes to git's index.
        
            git add -A

    4. Commit your changes.
        
            git commit -m "<description of work>"
        
    5. if (moreWorkToDo) go to #3.1 else go to #4.

4.  __Integrate changes from other developers.__ 
    Now you're finished with your feature or unit of work, and ready to push your changes, 
    you need to integrate changes that other developers may have pushed chances since you 
    started.

    __PRO TIP: CLOSE VISUAL STUDIO__
    
    There are a few steps to follow to make sure you keep the history clean as you integrate.
    
    1.  __Fetch changes from origin.__
        Use `git fetch` instead of `git pull`, because `git pull` automatically tries to merge the 
        new changes with your local commits, creating an ugly (and useless) merge commit.
        
            git fetch origin
        
    2.  __Rebase your topic branch against origin/master.__
        You want to reolcated your changes on top of any changes that were pulled in the
        in the fetch, above. You need to do this against origin/master instead of 
        master, because master isn't yet up to date (remember, you're still in your
        topic branch).

        You might have rebase conflicts, in which case you'll need to resolve them before
        continuing with `git rebase --continue`. You might want to use `git mergetool` to help.
        
            git rebase origin/master
        
    3.  __Test your changes with the new code integrated.__
        This would be a good time to run your full suite of unit and integration tests.
        
            git clean -xdf
            .\Build-Solution.ps1
            
        The first command cleans any untracked files that could get in the way of a good commit.

5.  __Integrate your changes into the master branch.__
    Now that your topic branch has a clean history, it's easy to use `git rebase` to integrate
    your changes into the master branch with the following three commands. Note that the 
    `git pull` will not create a merge commit, provided you haven't made any changes on master
    (e.g., that you followed the advice of making all changes in your topic branch).
    
        git checkout master
        git pull
        git rebase <topic branch>
    
6.  __Push changes.__
    Now that you're master branch's history is correct and clean, you can push to origin.
    
        git push origin

7.  __Delete the topic branch__
    The topic branch you created in Step #2 is no longer needed so it's best to delete it and 
    start with a new clean branch when you're ready to start your next unit of work.
    
        git branch -d <topic branch>
