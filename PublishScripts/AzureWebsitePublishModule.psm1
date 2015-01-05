#  AzureWebSitePublishModule.psm1 is a Windows PowerShell script module. This module exports Windows PowerShell functions that automate life cycle management for web applications. You can use the functions as is or customize them for your application and publishing environment.

Set-StrictMode -Version 3

# A variable to save original subscription.
$Script:originalCurrentSubscription = $null

# A variable to save original storage account.
$Script:originalCurrentStorageAccount = $null

# A variable to save storage account of user specified subscription.
$Script:originalStorageAccountOfUserSpecifiedSubscription = $null

# A variable to save subscription name.
$Script:userSpecifiedSubscription = $null


<#
.SYNOPSIS
Prepends the date and time to a message.

.DESCRIPTION
Prepends the date and time to a message. This function is designed for messages written to the Error and Verbose streams.

.PARAMETER  Message
Specifies the messages without the date.

.INPUTS
System.String

.OUTPUTS
System.String

.EXAMPLE
PS C:\> Format-DevTestMessageWithTime -Message "Adding file $filename to the directory"
2/5/2014 1:03:08 PM - Adding file $filename to the directory

.LINK
Write-VerboseWithTime

.LINK
Write-ErrorWithTime
#>
function Format-DevTestMessageWithTime
{
    [CmdletBinding()]
    param
    (
        [Parameter(Position=0, Mandatory = $true, ValueFromPipeline = $true)]
        [String]
        $Message
    )

    return ((Get-Date -Format G)  + ' - ' + $Message)
}


<#

.SYNOPSIS
Writes an error message prefixed with the current time.

.DESCRIPTION
Writes an error message prefixed with the current time. This function calls the Format-DevTestMessageWithTime function to prepend the time before writing the message to the Error stream.

.PARAMETER  Message
Specifies the message in the error message call. You can pipe the message string to the function.

.INPUTS
System.String

.OUTPUTS
None. The function writes to the Error stream.

.EXAMPLE
PS C:> Write-ErrorWithTime -Message "Failed. Cannot find the file."

Write-Error: 2/6/2014 8:37:29 AM - Failed. Cannot find the file.
 + CategoryInfo     : NotSpecified: (:) [Write-Error], WriteErrorException
 + FullyQualifiedErrorId : Microsoft.PowerShell.Commands.WriteErrorException

.LINK
Write-Error

#>
function Write-ErrorWithTime
{
    [CmdletBinding()]
    param
    (
        [Parameter(Position=0, Mandatory = $true, ValueFromPipeline = $true)]
        [String]
        $Message
    )

    $Message | Format-DevTestMessageWithTime | Write-Error
}


<#
.SYNOPSIS
Writes a verbose message prefixed with the current time.

.DESCRIPTION
Writes a verbose message prefixed with the current time. Because it calls Write-Verbose, the message displays only when the script runs with the Verbose parameter or when the VerbosePreference preference is set to Continue.

.PARAMETER  Message
Specifies the message in the verbose message call. You can pipe the message string to the function.

.INPUTS
System.String

.OUTPUTS
None. The function writes to the Verbose stream.

.EXAMPLE
PS C:> Write-VerboseWithTime -Message "The operation succeeded."
PS C:>
PS C:\> Write-VerboseWithTime -Message "The operation succeeded." -Verbose
VERBOSE: 1/27/2014 11:02:37 AM - The operation succeeded.

.EXAMPLE
PS C:\ps-test> "The operation succeeded." | Write-VerboseWithTime -Verbose
VERBOSE: 1/27/2014 11:01:38 AM - The operation succeeded.

.LINK
Write-Verbose
#>
function Write-VerboseWithTime
{
    [CmdletBinding()]
    param
    (
        [Parameter(Position=0, Mandatory = $true, ValueFromPipeline = $true)]
        [String]
        $Message
    )

    $Message | Format-DevTestMessageWithTime | Write-Verbose
}


<#
.SYNOPSIS
Writes a host message prefixed with the current time.

.DESCRIPTION
This function writes a message to the host program (Write-Host) prefixed with the current time. The effect of writing to the host program varies. Most programs that host Windows PowerShell write these messages to standard output.

.PARAMETER  Message
Specifies the base message without the date. You can pipe the message string to the function.

.INPUTS
System.String

.OUTPUTS
None. The function writes the message to the host program.

.EXAMPLE
PS C:> Write-HostWithTime -Message "The operation succeeded."
1/27/2014 11:02:37 AM - The operation succeeded.

.LINK
Write-Host
#>
function Write-HostWithTime
{
    [CmdletBinding()]
    param
    (
        [Parameter(Position=0, Mandatory = $true, ValueFromPipeline = $true)]
        [String]
        $Message
    )
    
    if ((Get-Variable SendHostMessagesToOutput -Scope Global -ErrorAction SilentlyContinue) -and $Global:SendHostMessagesToOutput)
    {
        if (!(Get-Variable -Scope Global AzureWebAppPublishOutput -ErrorAction SilentlyContinue) -or !$Global:AzureWebAppPublishOutput)
        {
            New-Variable -Name AzureWebAppPublishOutput -Value @() -Scope Global -Force
        }

        $Global:AzureWebAppPublishOutput += $Message | Format-DevTestMessageWithTime
    }
    else 
    {
        $Message | Format-DevTestMessageWithTime | Write-Host
    }
}


<#
.SYNOPSIS
Returns $true if a property or method is a member of the object. Otherwise, $false.

.DESCRIPTION
Returns $true if the property or method is a member of the object. This function returns $false for static methods of the class and for views, such as PSBase and PSObject.

.PARAMETER  Object
Specifies the object in the test. Enter a variable that contains an object or an expression that returns an object. You cannot specify types, such as [DateTime] or pipe objects to this function.

.PARAMETER  Member
Specifies the name of the property or method in the test. When specifying a method, omit parentheses that follow the method name.

.INPUTS
None. This function does not take input from the pipeline.

.OUTPUTS
System.Boolean

.EXAMPLE
PS C:\> Test-Member -Object (Get-Date) -Member DayOfWeek
True

.EXAMPLE
PS C:\> $date = Get-Date
PS C:\> Test-Member -Object $date -Member AddDays
True

.EXAMPLE
PS C:\> [DateTime]::IsLeapYear((Get-Date).Year)
True
PS C:\> Test-Member -Object (Get-Date) -Member IsLeapYear
False

.LINK
Get-Member
#>
function Test-Member
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [Object]
        $Object,

        [Parameter(Mandatory = $true)]
        [String]
        $Member
    )

    return $null -ne ($Object | Get-Member -Name $Member)
}


<#
.SYNOPSIS
Returns $true if the version of the Azure module is 0.7.4 or later. Else, $false.

.DESCRIPTION
Test-AzureModuleVersion returns $true if the version of the Azure module is 0.7.4 or later. It returns $false if the module isn't installed or is an earlier version. This function has no parameters.

.INPUTS
None

.OUTPUTS
System.Boolean

.EXAMPLE
PS C:\> Get-Module Azure -ListAvailable
PS C:\> #No module
PS C:\> Test-AzureModuleVersion
False

.EXAMPLE
PS C:\> (Get-Module Azure -ListAvailable).Version

Major  Minor  Build  Revision
-----  -----  -----  --------
0      7      4      -1

PS C:\> Test-AzureModuleVersion
True

.LINK
Get-Module

.LINK
PSModuleInfo object (http://msdn.microsoft.com/en-us/library/system.management.automation.psmoduleinfo(v=vs.85).aspx)
#>
function Test-AzureModuleVersion
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [System.Version]
        $Version
    )

    return ($Version.Major -gt 0) -or ($Version.Minor -gt 7) -or ($Version.Minor -eq 7 -and $Version.Build -ge 4)
}


<#
.SYNOPSIS
Returns $true if the installed Azure module version is 0.7.4 or later.

.DESCRIPTION
Test-AzureModule returns $true if the installed Azure module version is 0.7.4 or later. Returns $false if the module isn't installed or is an earlier version. This function has no parameters.

.INPUTS
None

.OUTPUTS
System.Boolean

.EXAMPLE
PS C:\> Get-Module Azure -ListAvailable
PS C:\> #No module
PS C:\> Test-AzureModule
False

.EXAMPLE
PS C:\> (Get-Module Azure -ListAvailable).Version

Major  Minor  Build  Revision
-----  -----  -----  --------
    0      7      4      -1

PS C:\> Test-AzureModule
True

.LINK
Get-Module

.LINK
PSModuleInfo object (http://msdn.microsoft.com/en-us/library/system.management.automation.psmoduleinfo(v=vs.85).aspx)
#>
function Test-AzureModule
{
    [CmdletBinding()]

    $module = Get-Module -Name Azure

    if (!$module)
    {
        $module = Get-Module -Name Azure -ListAvailable

        if (!$module -or !(Test-AzureModuleVersion $module.Version))
        {
            return $false;
        }
        else
        {
            $ErrorActionPreference = 'Continue'
            Import-Module -Name Azure -Global -Verbose:$false
            $ErrorActionPreference = 'Stop'

            return $true
        }
    }
    else
    {
        return (Test-AzureModuleVersion $module.Version)
    }
}


<#
.SYNOPSIS
Saves the current Microsoft Azure subscription in the $Script:originalSubscription variable in script scope.

.DESCRIPTION
The Backup-Subscription function saves the current Microsoft Azure subscription (Get-AzureSubscription -Current) and its storage account, and the subscription that is changed by this script ($UserSpecifiedSubscription) and its storage account, in script scope. By saving the values, you can use a function, such as Restore-Subscription, to restore the original current subscription and storage account to current status if the current status has changed.

.PARAMETER UserSpecifiedSubscription
Specifies the name of the subscription in which the new resources will be created and published. The function saves the names of the subscription and its storage accounts in script scope. This parameter is required.

.INPUTS
None

.OUTPUTS
None

.EXAMPLE
PS C:\> Backup-Subscription -UserSpecifiedSubscription Contoso
PS C:\>

.EXAMPLE
PS C:\> Backup-Subscription -UserSpecifiedSubscription Contoso -Verbose
VERBOSE: Backup-Subscription: Start
VERBOSE: Backup-Subscription: Original subscription is Microsoft Azure MSDN - Visual Studio Ultimate
VERBOSE: Backup-Subscription: End
#>
function Backup-Subscription
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]
        $UserSpecifiedSubscription
    )

    Write-VerboseWithTime 'Backup-Subscription: Start'

    $Script:originalCurrentSubscription = Get-AzureSubscription -Current -ErrorAction SilentlyContinue
    if ($Script:originalCurrentSubscription)
    {
        Write-VerboseWithTime ('Backup-Subscription: Original subscription is ' + $Script:originalCurrentSubscription.SubscriptionName)
        $Script:originalCurrentStorageAccount = $Script:originalCurrentSubscription.CurrentStorageAccountName
    }
    
    $Script:userSpecifiedSubscription = $UserSpecifiedSubscription
    if ($Script:userSpecifiedSubscription)
    {        
        $userSubscription = Get-AzureSubscription -SubscriptionName $Script:userSpecifiedSubscription -ErrorAction SilentlyContinue
        if ($userSubscription)
        {
            $Script:originalStorageAccountOfUserSpecifiedSubscription = $userSubscription.CurrentStorageAccountName
        }        
    }

    Write-VerboseWithTime 'Backup-Subscription: End'
}


<#
.SYNOPSIS
Restores to "current" status the Microsoft Azure subscription that is saved in the $Script:originalSubscription variable in script scope.

.DESCRIPTION
The Restore-Subscription function makes the subscription that is saved in the $Script:originalSubscription variable the current subscription (again). If the original subscription has a storage account, this function makes that storage account current for the current subscription.  The function restores the subscription only if there is a non-null $SubscriptionName variable in the environment. Otherwise, it exits.  If the $SubscriptionName is populated, but $Script:originalSubscription is $null, Restore-Subscription uses the Select-AzureSubscription cmdlet to clear the Current and Default settings for subscriptions in Microsoft Azure PowerShell.  This function doesn't have parameters, it takes no input, and it returns nothing (void). You can use -Verbose to write messages to the Verbose stream.

.INPUTS
None

.OUTPUTS
None

.EXAMPLE
PS C:\> Restore-Subscription
PS C:\>

.EXAMPLE
PS C:\> Restore-Subscription -Verbose
VERBOSE: Restore-Subscription: Start
VERBOSE: Restore-Subscription: End
#>
function Restore-Subscription
{
    [CmdletBinding()]
    param()

    Write-VerboseWithTime 'Restore-Subscription: Start'

    if ($Script:originalCurrentSubscription)
    {
        if ($Script:originalCurrentStorageAccount)
        {
            Set-AzureSubscription `
                -SubscriptionName $Script:originalCurrentSubscription.SubscriptionName `
                -CurrentStorageAccountName $Script:originalCurrentStorageAccount
        }

        Select-AzureSubscription -SubscriptionName $Script:originalCurrentSubscription.SubscriptionName
    }
    else 
    {
        Select-AzureSubscription -NoCurrent
        Select-AzureSubscription -NoDefault
    }
    
    if ($Script:userSpecifiedSubscription -and $Script:originalStorageAccountOfUserSpecifiedSubscription)
    {
        Set-AzureSubscription `
            -SubscriptionName $Script:userSpecifiedSubscription `
            -CurrentStorageAccountName $Script:originalStorageAccountOfUserSpecifiedSubscription
    }

    Write-VerboseWithTime 'Restore-Subscription: End'
}


<#
.SYNOPSIS
Validates the config file and returns a hashtable of config file values.

.DESCRIPTION
The Read-ConfigFile function validates the JSON configuration file and returns a hash table of selected values.
-- It begins by converting the JSON file to a PSCustomObject. The web site hash table has the following keys:
-- Location: Web site location
-- Databases: Web site SQL databases

.PARAMETER  ConfigurationFile
Specifies the path and name of the JSON configuration file for your web project. Visual Studio generates the JSON file automatically when you create a web project and stores it in the PublishScripts folder in your solution.

.PARAMETER HasWebDeployPackage
Indicates that there is a web deploy package ZIP file for the web application. To specify a value of $true, use -HasWebDeployPackage or HasWebDeployPackage:$true. To specify a value of false, use HasWebDeployPackage:$false.This parameter is required.

.INPUTS
None. You cannot pipe input to this function.

.OUTPUTS
System.Collections.Hashtable

.EXAMPLE
PS C:\> Read-ConfigFile -ConfigurationFile <path> -HasWebDeployPackage


Name                           Value                                                                                                                                                                     
----                           -----                                                                                                                                                                     
databases                      {@{connectionStringName=; databaseName=; serverName=; user=; password=}}                                                                                                  
website                        @{name="mysite"; location="West US";}                                                      
#>
function Read-ConfigFile
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateScript({Test-Path $_ -PathType Leaf})]
        [String]
        $ConfigurationFile
    )

    Write-VerboseWithTime 'Read-ConfigFile: Start'

    # Get the contents of the JSON file (-raw ignores line breaks) and convert it to a PSCustomObject
    $config = Get-Content $ConfigurationFile -Raw | ConvertFrom-Json

    if (!$config)
    {
        throw ('Read-ConfigFile: ConvertFrom-Json failed: ' + $error[0])
    }

    # Determine whether the environmentSettings object has 'webSite' properties (regardless of the property value)
    $hasWebsiteProperty =  Test-Member -Object $config.environmentSettings -Member 'webSite'

    if (!$hasWebsiteProperty)
    {
        throw 'Read-ConfigFile: The configuration file does not have a webSite property.'
    }

    # Build a hash table from the values in the PSCustomObject
    $returnObject = New-Object -TypeName Hashtable

    $returnObject.Add('name', $config.environmentSettings.webSite.name)
    $returnObject.Add('location', $config.environmentSettings.webSite.location)

    if (Test-Member -Object $config.environmentSettings -Member 'databases')
    {
        $returnObject.Add('databases', $config.environmentSettings.databases)
    }

    Write-VerboseWithTime 'Read-ConfigFile: End'

    return $returnObject
}


<#
.SYNOPSIS
Creates a Microsoft Azure web site.

.DESCRIPTION
Creates a Microsoft Azure web site with the specific name and location. This function calls the New-AzureWebsite cmdlet in the Azure module. If the subscription does not yet have a web site with the specified name, this function creates the web site and returns a web site object. Otherwise, it returns the existing web site.

.PARAMETER  Name
Specifies a name for the new web site. The name must be unique in Microsoft Azure. This parameter is required.

.PARAMETER  Location
Specifies the location of the web site. Valid values are the Microsoft Azure locations, such as "West US". This parameter is required.

.INPUTS
NONE.

.OUTPUTS
Microsoft.WindowsAzure.Commands.Utilities.Websites.Services.WebEntities.Site

.EXAMPLE
Add-AzureWebsite -Name TestSite -Location "West US"

Name       : contoso
State      : Running
Host Names : contoso.azurewebsites.net

.LINK
New-AzureWebsite
#>
function Add-AzureWebsite
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [String]
        $Name,

        [Parameter(Mandatory = $true)]
        [String]
        $Location
    )

    Write-VerboseWithTime 'Add-AzureWebsite: Start'
    $website = Get-AzureWebsite -Name $Name -ErrorAction SilentlyContinue

    if ($website)
    {
        Write-HostWithTime ('Add-AzureWebsite: An existing web site ' +
        $website.Name + ' was found')
    }
    else
    {
        if (Test-AzureName -Website -Name $Name)
        {
            Write-ErrorWithTime ('Website {0} already exists' -f $Name)
        }
        else
        {
            $website = New-AzureWebsite -Name $Name -Location $Location
        }
    }

    $website | Out-String | Write-VerboseWithTime
    Write-VerboseWithTime 'Add-AzureWebsite: End'

    return $website
}

<#
.SYNOPSIS
Returns $True when the URL is absolute and its scheme is https.

.DESCRIPTION
The Test-HttpsUrl function converts the input URL to a System.Uri object. Returns $True when the URL is absolute (not relative) and its scheme is https. If either is false, or the input string cannot be converted to a URL, the function returns $false.

.PARAMETER Url
Specifies the URL to test. Enter a URL string,

.INPUTS
NONE.

.OUTPUTS
System.Boolean

.EXAMPLE
PS C:\>$profile.publishUrl
waws-prod-bay-001.publish.azurewebsites.windows.net:443

PS C:\>Test-HttpsUrl -Url 'waws-prod-bay-001.publish.azurewebsites.windows.net:443'
False
#>
function Test-HttpsUrl
{

    param
    (
        [Parameter(Mandatory = $true)]
        [String]
        $Url
    )

    # If $uri cannot be converted to a System.Uri object, Test-HttpsUrl returns $false
    $uri = $Url -as [System.Uri]

    return $uri.IsAbsoluteUri -and $uri.Scheme -eq 'https'
}


<#
.SYNOPSIS
Creates a string that lets you connect to a Microsoft Azure SQL database.

.DESCRIPTION
The Get-AzureSQLDatabaseConnectionString function assembles a connection string to connect to a Microsoft Azure SQL database.

.PARAMETER  DatabaseServerName
Specifies the name of an existing database server in the Microsoft Azure subscription. All Microsoft Azure SQL databases must be associated with a SQL database server. To get the server name, use the Get-AzureSqlDatabaseServer cmdlet (Azure module). This parameter is required.

.PARAMETER  DatabaseName
Specifies the name for the SQL database. This can be an existing SQL database or a name used for a new SQL database. This parameter is required.

.PARAMETER  Username
Specifies the name of the SQL database administrator. The username will be $Username@DatabaseServerName. This parameter is required.

.PARAMETER  Password
Specifies a password for the SQL database administrator. Enter a password in plain text. Secure strings are not permitted. This parameter is required.

.INPUTS
None.

.OUTPUTS
System.String

.EXAMPLE
PS C:\> $ServerName = (Get-AzureSqlDatabaseServer).ServerName[0]
PS C:\> Get-AzureSQLDatabaseConnectionString -DatabaseServerName $ServerName `
        -DatabaseName 'testdb' -UserName 'admin'  -Password 'password'

Server=tcp:testserver.database.windows.net,1433;Database=testdb;User ID=admin@testserver;Password=password;Trusted_Connection=False;Encrypt=True;Connection Timeout=20;
#>
function Get-AzureSQLDatabaseConnectionString
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [String]
        $DatabaseServerName,

        [Parameter(Mandatory = $true)]
        [String]
        $DatabaseName,

        [Parameter(Mandatory = $true)]
        [String]
        $UserName,

        [Parameter(Mandatory = $true)]
        [String]
        $Password
    )

    return ('Server=tcp:{0}.database.windows.net,1433;Database={1};' +
           'User ID={2}@{0};' +
           'Password={3};' +
           'Trusted_Connection=False;' +
           'Encrypt=True;' +
           'Connection Timeout=20;') `
           -f $DatabaseServerName, $DatabaseName, $UserName, $Password
}


<#
.SYNOPSIS
Creates Microsoft Azure SQL databases from the values in the JSON configuation file that Visual Studio generates.

.DESCRIPTION
The Add-AzureSQLDatabases function takes information from the databases section of the JSON file. This function, Add-AzureSQLDatabases (plural), calls the Add-AzureSQLDatabase (singular) function for each SQL database in the JSON file. Add-AzureSQLDatabase (singular) calls the New-AzureSqlDatabase cmdlet (Azure module), which creates the SQL databases. This function does not return a database object. It returns a hashtable of values that were used to create the databases.

.PARAMETER DatabaseConfig
 Takes an array of PSCustomObjects that originate in the JSON file that the Read-ConfigFile function returns when the JSON file has a web site property. It includes the environmentSettings.databases properties. You can pipe the list to this function.
PS C:\> $config = Read-ConfigFile <name>.json
PS C:\> $DatabaseConfig = $config.databases| where {$_.connectionStringName}
PS C:\> $DatabaseConfig
connectionStringName: Default Connection
databasename : TestDB1
edition   :
size     : 1
collation  : SQL_Latin1_General_CP1_CI_AS
servertype  : New SQL Database Server
servername  : r040tvt2gx
user     : dbuser
password   : Test.123
location   : West US

.PARAMETER  DatabaseServerPassword
Specifies the password for the SQL database server administrator. Enter a hashtable with Name and Password keys. The value of Name is the name of the SQL database server. The value of Password is the administrator password. For example: @Name = "TestDB1"; Password = "password" This parameter is optional. If you omit it or the SQL database server name doesn't match the value of the serverName property of the $DatabaseConfig object, the function uses the Password property of the $DatabaseConfig object for the SQL database in the connection string.

.PARAMETER CreateDatabase
Verifies that you want to create a database. This parameter is optional.

.INPUTS
System.Collections.Hashtable[]

.OUTPUTS
System.Collections.Hashtable

.EXAMPLE
PS C:\> $config = Read-ConfigFile <name>.json
PS C:\> $DatabaseConfig = $config.databases| where {$_.connectionStringName}
PS C:\> $DatabaseConfig | Add-AzureSQLDatabases

Name                           Value
----                           -----
ConnectionString               Server=tcp:testdb1.database.windows.net,1433;Database=testdb;User ID=admin@testdb1;Password=password;Trusted_Connection=False;Encrypt=True;Connection Timeout=20;
Name                           Default Connection
Type                           SQLAzure

.LINK
Get-AzureSQLDatabaseConnectionString

.LINK
Create-AzureSQLDatabase
#>
function Add-AzureSQLDatabases
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [PSCustomObject]
        $DatabaseConfig,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [Hashtable[]]
        $DatabaseServerPassword,

        [Parameter(Mandatory = $false)]
        [Switch]
        $CreateDatabase = $false
    )

    begin
    {
        Write-VerboseWithTime 'Add-AzureSQLDatabases: Start'
    }
    process
    {
        Write-VerboseWithTime ('Add-AzureSQLDatabases: Creating ' + $DatabaseConfig.databaseName)

        if ($CreateDatabase)
        {
            # Creates a new SQL database with the DatabaseConfig values (unless one already exists)
            # The command output is suppressed.
            Add-AzureSQLDatabase -DatabaseConfig $DatabaseConfig | Out-Null
        }

        $serverPassword = $null
        if ($DatabaseServerPassword)
        {
            foreach ($credential in $DatabaseServerPassword)
            {
               if ($credential.Name -eq $DatabaseConfig.serverName)
               {
                   $serverPassword = $credential.password             
                   break
               }
            }               
        }

        if (!$serverPassword)
        {
            $serverPassword = $DatabaseConfig.password
        }

        return @{
            Name = $DatabaseConfig.connectionStringName;
            Type = 'SQLAzure';
            ConnectionString = Get-AzureSQLDatabaseConnectionString `
                -DatabaseServerName $DatabaseConfig.serverName `
                -DatabaseName $DatabaseConfig.databaseName `
                -UserName $DatabaseConfig.user `
                -Password $serverPassword }
    }
    end
    {
        Write-VerboseWithTime 'Add-AzureSQLDatabases: End'
    }
}


<#
.SYNOPSIS
Creates a new Microsoft Azure SQL database.

.DESCRIPTION
The Add-AzureSQLDatabase function creates a Microsoft Azure SQL database from the data in the JSON configuration file that Visual Studio generates and returns the new database. If the subscription already has a SQL database with the specified database name in the specified SQL database server, the function returns the existing database. This function calls the New-AzureSqlDatabase cmdlet (Azure module), which actually creates the SQL database.

.PARAMETER DatabaseConfig
Takes a PSCustomObject that originates in the JSON configuration file that the Read-ConfigFile function returns when the JSON file has a web site property. It includes the environmentSettings.databases properties. You cannot pipe the object to this function. Visual Studio generates a JSON configuration file for all web projects and stores it in the PublishScripts folder of your solution.

.INPUTS
None. This function does not take input from the pipeline

.OUTPUTS
Microsoft.WindowsAzure.Commands.SqlDatabase.Services.Server.Database

.EXAMPLE
PS C:\> $config = Read-ConfigFile <name>.json
PS C:\> $DatabaseConfig = $config.databases | where connectionStringName
PS C:\> $DatabaseConfig

connectionStringName    : Default Connection
databasename : TestDB1
edition      :
size         : 1
collation    : SQL_Latin1_General_CP1_CI_AS
servertype   : New SQL Database Server
servername   : r040tvt2gx
user         : dbuser
password     : Test.123
location     : West US

PS C:\> Add-AzureSQLDatabase -DatabaseConfig $DatabaseConfig

.LINK
Add-AzureSQLDatabases

.LINK
New-AzureSQLDatabase
#>
function Add-AzureSQLDatabase
{
    [CmdletBinding()]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNull()]
        [Object]
        $DatabaseConfig
    )

    Write-VerboseWithTime 'Add-AzureSQLDatabase: Start'

    # Fail if the parameter value doesn't have the serverName property, or the serverName property value isn't populated.
    if (-not (Test-Member $DatabaseConfig 'serverName') -or -not $DatabaseConfig.serverName)
    {
        throw 'Add-AzureSQLDatabase: The database serverName (required) is missing from the DatabaseConfig value.'
    }

    # Fail if the parameter value doesn't have the databasename property, or the databasename property value isn't populated.
    if (-not (Test-Member $DatabaseConfig 'databaseName') -or -not $DatabaseConfig.databaseName)
    {
        throw 'Add-AzureSQLDatabase: The databasename (required) is missing from the DatabaseConfig value.'
    }

    $DbServer = $null

    if (Test-HttpsUrl $DatabaseConfig.serverName)
    {
        $absoluteDbServer = $DatabaseConfig.serverName -as [System.Uri]
        $subscription = Get-AzureSubscription -Current -ErrorAction SilentlyContinue

        if ($subscription -and $subscription.ServiceEndpoint -and $subscription.SubscriptionId)
        {
            $absoluteDbServerRegex = 'https:\/\/{0}\/{1}\/services\/sqlservers\/servers\/(.+)\.database\.windows\.net\/databases' -f `
                                     $subscription.serviceEndpoint.Host, $subscription.SubscriptionId

            if ($absoluteDbServer -match $absoluteDbServerRegex -and $Matches.Count -eq 2)
            {
                 $DbServer = $Matches[1]
            }
        }
    }

    if (!$DbServer)
    {
        $DbServer = $DatabaseConfig.serverName
    }

    $db = Get-AzureSqlDatabase -ServerName $DbServer -DatabaseName $DatabaseConfig.databaseName -ErrorAction SilentlyContinue

    if ($db)
    {
        Write-HostWithTime ('Create-AzureSQLDatabase: Using existing database ' + $db.Name)
        $db | Out-String | Write-VerboseWithTime
    }
    else
    {
        $param = New-Object -TypeName Hashtable
        $param.Add('serverName', $DbServer)
        $param.Add('databaseName', $DatabaseConfig.databaseName)

        if ((Test-Member $DatabaseConfig 'size') -and $DatabaseConfig.size)
        {
            $param.Add('MaxSizeGB', $DatabaseConfig.size)
        }
        else
        {
            $param.Add('MaxSizeGB', 1)
        }

        # If the $DatabaseConfig object has a collation property and it's not null or empty
        if ((Test-Member $DatabaseConfig 'collation') -and $DatabaseConfig.collation)
        {
            $param.Add('Collation', $DatabaseConfig.collation)
        }

        # If the $DatabaseConfig object has an edition property and it's not null or empty
        if ((Test-Member $DatabaseConfig 'edition') -and $DatabaseConfig.edition)
        {
            $param.Add('Edition', $DatabaseConfig.edition)
        }

        # Write the hash table to the Verbose stream
        $param | Out-String | Write-VerboseWithTime
        # Call New-AzureSqlDatabase with splatting (suppress the output)
        $db = New-AzureSqlDatabase @param
    }

    Write-VerboseWithTime 'Add-AzureSQLDatabase: End'
    return $db
}
