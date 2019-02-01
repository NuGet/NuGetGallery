Function Install-DailyTask
{
    $trigger = New-ScheduledTaskTrigger -DaysInterval 1 -At "12pm" -Daily

    Install-NuGetScheduledTask $trigger "Support Request Daily Notification" "OnCallDailyNotification.cmd"
}

Function Install-WeeklyTask
{
    $trigger = New-ScheduledTaskTrigger -Weekly -WeeksInterval 1 -DaysOfWeek Monday -At "12pm"

    Install-NuGetScheduledTask $trigger "Support Requests Weekly Notification" "WeeklySummaryNotification.cmd"
}

Function Install-NuGetScheduledTask
{
    param($Trigger, $Name, $Command)

    #Action to run as
    $cmdExe = [system.environment]::getenvironmentvariable("ComSpec")
    $action = New-ScheduledTaskAction -Execute $cmdExe -Argument "/c $PSScriptRoot\$Command" -WorkingDirectory $PSScriptRoot

    #Configure when to stop the task and how long it can run for. In this example it does not stop on idle and uses the maximum possible duration by setting a timelimit of 0
    $settings = New-ScheduledTaskSettingsSet -DontStopOnIdleEnd -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew

    #Configure the principal to use for the scheduled task and the level to run as
    $principal = New-ScheduledTaskPrincipal -UserID "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel "Highest"

    #Register the new scheduled task
    Register-ScheduledTask -TaskName $Name -Action $action -Trigger $Trigger -Principal $principal -Settings $settings -Force
}
