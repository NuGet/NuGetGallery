This is a re-write of the NuGet Gallery.

## Getting Started

The Build-Solution.ps1 script will build the solution, run the facts (unit tests), and update the database (from migrations).

## The Git Workflow

This is the Git workflow we're currently using:

    # start a new feature/unit of work
    git pull # assumes no un-pushed changes in your local repo, so no merge
    git checkout -b <topic branch>
        # repeat the following as you make changes for the feature/unit of work  
        <do work>
        <run focused tests>
        git add .
        git commit -m "<description of work>"
    # you're ready to push the feature/unit of work
    <run all tests>
    git fetch origin # we don't pull, because we don't want to clutter history with merges
    git rebase origin/master # this won't be needed if there aren't new commits from origin in the step above
    git checkout master
    git pull # because we rebased, there won't be a merge
    git rebase <topic branch>
    git push