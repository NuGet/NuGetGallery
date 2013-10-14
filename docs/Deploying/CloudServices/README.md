# Deploying the NuGet Gallery to Windows Azure Cloud Services

## Setting up resources
To run the NuGet Gallery on Windows Azure Cloud Services you need to provision the following Azure resources:

1. An Azure Cloud Service
2. An Azure SQL Database (preferably in a dedicated Azure SQL Server) to hold the package metadata
3. An Azure Storage account to hold package files, diagnostics data, etc.

It is assumed you've already provisioned the SQL Database and Storage Account using the [main guide](../README.md).

## Provisioning the Frontend
Create an Azure Cloud Service. Poof, done. Ok, not quite. Grab the [src\NuGetGallery.Cloud\ServiceConfiguration.Local.cscfg](../../../src/NuGetGallery.Cloud/ServiceConfiguration.Local.cscfg) file and copy it to somewhere **OUTSIDE** the repository. Give it a name that you'll remember like "MyNuGetGallery.TestEnvironment.cscfg". Update the settings as per the comments inside the file using the connection strings you received while provisioning resources for the site (SQL DB, Azure Storage, etc.).

Save this file somewhere safe and **SECURE** as it now has passwords for your database inside!

## Deploying the Service
These steps should be repeated each time you have new code to deploy.

### Migrate the Database
Migrate the database by running "Update-Database" from the Package Manager Console in a Visual Studio session with NuGetGallery.sln. You must select "NuGetGallery" as the default project. Use the "-ConnectionString" parameter to specify the connection string to the target database.

We attempt to ensure most migrations are additive, but please verify this before migrating your database. Our development process attempts to ensure that old code can read new data for a short transition period. If you keep your code up to date with ours (we deploy approximately fortnightly) then you should be able to match this process.

### Updating Service Credentials
We highly recommend updating service credentials (SQL passwords, Azure Storage keys, etc.) on each deployment to reduce the risk of credential leakage. Some incomplete notes on doing this are listed below:

1. Use the CreateSqlUser task in the 'galops' executable (implemented in NuGetGallery.Operations) to create a SQL User suitable for the Gallery Frontend and update the SQL Connection String for the Frontend to use it. NOTE: DO NOT remove the old user until the old code is offline. ALSO: DO NOT use this user in the Backend as the Backend requires administrator permissions.
2. Regenerate the Azure Storage keys that are NOT CURRENTLY BEING USED by the live service and update the following settings with it. NOTE: There are multiple storage accounts (Primary, Backup and optionally Diagnostics). All storage account keys should be refreshed like this.

### Deploy the package
1. Click on the cloud service
2. Click "Staging" at the top
3. Click "Update" at the bottom
4. Upload/Select from blob storage the CSCFG and CSPKG files produced by building the solution
5. Select "ALL" roles and check all the checkboxes
6. Enter a meaningful name for the deployment (i.e. "Oct14 @ 1529 (deadbeef21)", where deadbeef21 is the commit hash of the build being used)
7. GO!
8. Once deployment completes, verify the staging URL
9. If the staging URL checks out, VIP Swap!
10. Repeat steps 1-7 to deploy the same code to the Staging slot so you can easily make configuration changes and VIP Swap to reduce downtime.