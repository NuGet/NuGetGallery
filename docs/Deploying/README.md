# Deploying the NuGet Gallery

To run the NuGet Gallery in Azure you need to provision the following resources:

1. A SQL Database to hold the package metadata.
2. A location in which to store package files. The Gallery supports two at the moment: Local File System and Azure Storage Account.
3. A Web Frontend to host the Gallery.

## Deploying to Azure

We suggest using Azure to host the gallery, as that is the environment used by https://www.nuget.org itself. When doing so, we suggest using Azure SQL Databases for the database and Azure Storage to store packages.

This guide will instruct you on hosting the Gallery to an Azure App Service. We will start with provisiong the supporting resources (Database, Storage, etc.).

## Provisioning for Azure

### Provisioning a Database

We recommend provisioning a dedicated Azure SQL Databases Server for the Gallery.

Follow the instructions [here](https://docs.microsoft.com/en-us/azure/sql-database/sql-database-get-started-portal) to create an Azure SQL Database.

After you create your Azure SQL Database, update the server's firewall settings to allow access from other Azure resources:

1. Navigate to the "SQL Servers" blade.
2. Select your SQL Server.
3. Under the "Security" section, select "Firewall and and virtual networks".
4. Make sure that "Allow Azure services and resources to access this server" is set to "Yes".

Copy the connection string from the portal. It should look something like:

```
Server=[servername].database.windows.net;Database=NuGetGallery;User ID=[username];Password=[password];Trusted_Connection=False;Encrypt=True
```

Now, let's update your new DB with the Gallery SQL schema. 

1. Open the NuGetGallery solution in Visual Studio.

2. Open the [web.config](https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery/Web.config#L183) and replace the Gallery.SqlServer connection string with this value.

3. Expand the "Package Manager Console" tool window:

![Package Manager Console](images/03-PackageManagerConsole.png)

4. In the Package Manager console, type the following command:

```PowerShell
Update-Database -ConfigurationTypeName MigrationsConfiguration
```

### Provisioning Storage Accounts

Follow the instruction [here](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=portal) to create an Azure storage account.
Copy the connection string from the portal. It should like something like:

```
DefaultEndpointsProtocol=https;AccountName=[account name];AccountKey=[primary key];
```

To configure Gallery to use your new storage account:

1. Open the [web.config](https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery/Web.config#L27)
2. Set Gallery.StorageType to 'AzureStorage'
3. Replace all settings starting with 'Gallery.AzureStorage.' with your connection string.

## Deploying the Frontend/Backend

You are almost done! Here are additional configurations in web.config:

1. Gallery.SiteRoot - set with the URL of your Gallery website. For example: _https://mygallery.azurewebsites.net_
1. Gallery.AppInsightsInstrumentationKey - set to the [Application Insights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview) instrumentation key to capture telemetry. Useful for debugging!

You are now ready to publish the Gallery to your own Azure App Service. To do this through Visual Studio follow the instructions [here](https://docs.microsoft.com/en-us/visualstudio/deployment/quickstart-deploy-to-azure).
