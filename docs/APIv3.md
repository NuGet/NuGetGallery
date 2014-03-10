# Deploying the v2 Gallery in APIv3 land
The v2 Gallery can be deployed using mostly the same process as in v2. For each deployment, a new SQL user should be created using the following command:

```posh
nucmd db createuser -sv v2gallery -s dbo -db legacy -dc 0 -clip
```

**NOTE:** The server and database name should match the "Sql.Legacy" configuration value in the V3 config. The user name and password will be different.

This will place a new connection string in the clipboard, which you can use to update the "Gallery.SqlServer" setting.

Also, since the Work service and the V2Gallery service BOTH access the Legacy storage account, the Account Keys must be synchronized between the "Storage.Legacy" setting in the APIv3 service config and the "Gallery.AzureStorageConnectionString" value in the V2 config.
