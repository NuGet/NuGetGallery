# Deploying the NuGet Gallery to Windows Azure Websites

## Setting up resources
To run the NuGet Gallery on Windows Azure Websites you need to provision the following Azure resources:

1. An Azure Website running .Net 4.5 to run the gallery frontend
2. An Azure SQL Database (preferably in a dedicated Azure SQL Server) to hold the package metadata
3. An Azure Storage account to hold package files, diagnostics data, etc.
4. [Optional] An Azure Cloud Service to run the gallery backend worker
5. [Optional] An Azure Cloud Service to provide SSL Forwarding and Traffic Management to the gallery frontend

It is assumed you've already provisioned the SQL Database and Storage Account using the [main guide](../README.md).

## Provisioning the Frontend
Create an Azure Website and configure it for deployment however you choose (Git deployment, FTP, etc.).

Using the connection strings you received while provisioning resources for the site (SQL DB, Azure Storage, etc.), go to the Configure tab of the website and set the AppSettings. To determine what to set, open the [src\NuGetGallery\Web.config](../../../src/NuGetGallery/Web.config) file up and use the comments in the AppSettings section to assist you.

Save the changes and get ready to deploy!

## Migrate the Database
Migrate the database by running "Update-Database" from the Package Manager Console in a Visual Studio session with NuGetGallery.sln. You must select "NuGetGallery" as the default project. Use the "-ConnectionString" parameter to specify the connection string to the target database.

We attempt to ensure most migrations are additive, but please verify this before migrating your database. Our development process attempts to ensure that old code can read new data for a short transition period. If you keep your code up to date with ours (we deploy approximately fortnightly) then you should be able to match this process.

## Deploying the Frontend with Git
(If you want to use a different deployment mechanism, you're on your own :))

Go back to the dashboard tab and set up deployment from source control. Assuming you cloned this repo from Git, you probably want to choose "Local Git Repository". That is the case we will cover here.

Once you complete this wizard, you'll need to set up deployment credentials. I'll assume you've got that under control ;). Go to the gallery and checkout the branch you want to deploy (say 'master'):

```
git checkout master
```

Add Azure as remote and push that branch up!

```
git remote add azure [git url]
git push azure master
```

NOTE: If you don't use master, make sure to update the "Branch to Deploy" setting in the Configure tab.

After pushing, the site will build, which may take a while. Once finished, just browse to the site and it should start up! The first request may take quite a while, so be prepared to wait a few minutes.

Now that you've got the site up, try registering a user and uploading a package!

## Deploying the backend
TODO.
