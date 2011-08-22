This is an implementation of the NuGet Gallery and OData Package Feed.

## Getting Started

The Build-Solution.ps1 script will build the solution, run the facts (unit tests), and update the database (from migrations).

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
        
            .\Build-Solution.ps1

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
