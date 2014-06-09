# Deploying the Work Service

## Initial Provisioning
The work service requires the following resources:

1. A SQL Server with a database: 'nuget-[environment]-[dc]' (for example: 'nuget-int-0')
2. A Primary Storage Account with the name: 'nuget[environment][dc]' (for example: 'nugetint0')
3. An SSL Certificate for the target service
4. An RDP Certificate for the target service

Optional resources include:

1. The Connection String to the Legacy (APIv2) Storage account and Database
2. A Backup storage account
3. A Warehouse database connection string

To provision the service, create a Cloud Service in Azure with the name 'nuget-[environment]-[dc]-[service]' (for example: 'nuget-int-0-work') and upload the SSL and RDP certificates.

Provisioning the Primary SQL Database:

1. Create a database
2. Create two logins in the primary SQL Server with random passwords named 'primary' and 'secondary'
3. Publish the NuGet.Services.Work.Database project to the SQL Database
4. Create the two users using the following script (replace '[primary]' with '[secondary]' to create the second user):
	
	CREATE USER [primary] FROM LOGIN [primary]

5. Grant those users access to the work schema (replace '[primary]' with '[secondary]' to authorize the second user):

	GRANT CONTROL, ALTER ON SCHEMA :: [work] TO [primary]

6. Put primary's credentials in the CSCFG.

## Initial Configuration
1. Copy an existing config from the cscfg store
2. Update the 'Host.*' settings with the values for this service
3. Set the Sql.* and Storage.* settings as necessary
4. Add the thumbprints of the SSL and RDP certificates to the config
5. Use the Get-RemoteDesktopPassword function in the NuGet Ops console (".\ops" from the root of a NuGetGallery enlistment) to generate a new password. Update the Sharepoint site with the plaintext value and the cscfg with the ciphertext value.

## Per-Deployment Reconfiguration
1. Determine which Database user is going to be the 'target' user by looking at the current connection string. If it is 'primary', then you will be using 'secondary' in the below steps, otherwise, you'll be using 'primary' (this is key rotation, just like with Storage Accounts). In the below steps 'target' refers to the user you identified
  1. Change the password for the 'target' login on the SQL Server
  2. Update the cscfg with target's credentials
2. Rotate the Storage Account keys in the cscfg as well
  1. If the cscfg uses the Primary key, regenerate the Secondary and put it in the cscfg
  2. If the cscfg uses the Secondary key, regenerate the Primary and put it in the cscfg
3. Generate a new RDP Password using the Get-RemoteDesktopPassword function in the NuGet Ops console.
