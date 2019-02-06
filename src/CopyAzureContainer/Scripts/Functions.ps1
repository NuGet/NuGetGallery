Function Install-BackupV3Task
{
    Param ( [string]$triggersAt="12am")

    $STTrigger = New-ScheduledTaskTrigger -DaysInterval 1 -At $triggersAt -Daily 

    #Name and path for the scheduled task
    $STName = "Nuget\BackupV3Job"

    #Action to run as
    $cmdexe = [system.environment]::getenvironmentvariable("ComSpec")
    $STAction = New-ScheduledTaskAction -Execute $cmdexe -Argument "/c $PSScriptRoot\backupv3storage.cmd" -WorkingDirectory $PSScriptRoot

    #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
    $STSettings = New-ScheduledTaskSettingsSet -DontStopOnIdleEnd -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew

    #Configure the principal to use for the scheduled task and the level to run as
    $STPrincipal = New-ScheduledTaskPrincipal -UserID "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel "Highest"

    #Register the new scheduled task
    Register-ScheduledTask -TaskName $STName -Action $STAction -Trigger $STTrigger -Principal $STPrincipal -Settings $STSettings -Force
}

Function Install-AzCopy
{
    Param ( [string]$toolsPath="$PSScriptRoot\bin\tools\azcopy")

    $toolsPath
    $azcopy = "$toolsPath\azcopy.exe"

    if(Test-Path $azcopy)
    {
        Write-Output "AzCopy.exe has already been downloaded." 
    } 
    else 
    {
        $bootstrap = "$env:TEMP\azcopy_"+[System.Guid]::NewGuid()
        $output = "$bootstrap\extracted"
        $msi = "$bootstrap\MicrosoftAzureStorageTools.msi"

        Write-Output "Downloading AzCopy."
        Write-Output "Bootstrap directory: '$bootstrap'"

        mkdir $toolsPath -ErrorAction Ignore | Out-Null
        mkdir $bootstrap | Out-Null

        Invoke-WebRequest -Uri "http://aka.ms/downloadazcopy" -OutFile $msi
        Unblock-File $msi

        Write-Host "Extracting AzCopy"
        $proc = Start-Process msiexec -Argument "/a $msi /qb TARGETDIR=$output /quiet" -Wait -PassThru

        if ($proc.ExitCode -eq 0)
        {
            Copy-Item "$output\Microsoft SDKs\Azure\AzCopy\*" $toolsPath -Force
        }
        else
        {
            Write-Host "Install AzCopy failed!"
        }
        Remove-Item $bootstrap -Recurse -Force
    }
}
