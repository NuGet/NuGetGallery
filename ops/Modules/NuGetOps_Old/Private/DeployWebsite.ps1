function DeployWebsite($Service, $Package) {
    $MSDeployKey = 'HKLM:\SOFTWARE\Microsoft\IIS Extensions\MSDeploy\3'
    if(!(Test-Path $MSDeployKey)) {
       throw "Could not find MSDeploy. Use Web Platform Installer to install the 'Web Deployment Tool' and re-run this command"
    }
    $InstallPath = (Get-ItemProperty $MSDeployKey).InstallPath
    if(!$InstallPath -or !(Test-Path $InstallPath)) {
       throw "Could not find MSDeploy. Use Web Platform Installer to install the 'Web Deployment Tool' and re-run this command"
    }

    $MSDeploy = Join-Path $InstallPath "msdeploy.exe"
    if(!(Test-Path $MSDeploy)) {
       throw "Could not find MSDeploy. Use Web Platform Installer to install the 'Web Deployment Tool' and re-run this command"
    }

    $Site = RunInSubscription $Service.Environment.Subscription.Name {
        Write-Host "Downloading Site configuration for $($Service.ID)"
        Get-AzureWebsite $Service.ID
    }

    if(!$Site) {
        throw "Failed to load site: $($Service.ID)"
    }

    # MSDeploy Settings
    $UserName = "`$$($Site.Name)"
    $Password = $Site.PublishingPassword

    # HACK: Hack up the SelfLink to point at the publish endpoint
    $subdomain = $Site.SelfLink.Host.Split(".")[0]
    $PublishUrl = "https://$subdomain.publish.azurewebsites.windows.net:443/msdeploy.axd?Site=$($Site.Name)"

    # DEPLOY!
    Write-Host "Deploying package to $PublishUrl for $($Site.Name)"

    $arguments = [string[]]@(
        "-verb:sync",
        "-source:package='$Package'",
        "-dest:auto,computerName='$PublishUrl',userName='$UserName',password='$Password',authtype='Basic',includeAcls='False'",
        "-setParam:name='IIS Web Application Name',value='$($Site.Name)'")

    Write-Verbose "msdeploy $arguments"
    #&$msdeploy @arguments
    Start-Process $msdeploy -ArgumentList $arguments -NoNewWindow -Wait
}