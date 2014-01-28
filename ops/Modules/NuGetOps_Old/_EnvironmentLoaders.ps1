function Get-Subscriptions($NuGetOpsDefinition) {
    $Subscriptions = @{};
    $SubscriptionsList = Join-Path $NuGetOpsDefinition "Subscriptions.xml"
    if(Test-Path $SubscriptionsList) {
        $x = [xml](cat $SubscriptionsList)
        $x.subscriptions.subscription | ForEach-Object {
            # Get the subscription object
            $sub = $null;
            if($accounts.Length -gt 0) {
                $sub = Get-AzureSubscription $_.name
            }
            if($sub -eq $null) {
                Write-Warning "Could not find subscription $_ in Subscriptions.xml. Do you have access to it?"
            }

            $Subscriptions[$_.name] = New-Object PSCustomObject
            Add-Member -NotePropertyMembers @{
                Version = $NuGetOpsVersion;
                Id = $_.id;
                Name = $_.name;
                Subscription = $sub;
            } -InputObject $Subscriptions[$_.name]
        }
    } else {
        Write-Warning "Subscriptions list not found at $SubscriptionsList. No Subscriptions will be available."
    }

    $Subscriptions
}

function Get-V3Environments($NuGetOpsDefinition) {
    $Environments = @{};
    $Subscriptions = $null;

    $EnvironmentsList = Join-Path $NuGetOpsDefinition "Environments.v3.xml"
    if(!(Test-Path $EnvironmentsList)) {
        return
    }

    if($NuGetOpsDefinition -and (Test-Path $NuGetOpsDefinition)) {
        $Subscriptions = Get-Subscriptions -NuGetOpsDefinition $NuGetOpsDefinition

        $x = [xml](cat $EnvironmentsList);
        $x.environments.environment | ForEach-Object {
            $env = New-Object PSCustomObject
            $sub = $Subscriptions[$_.subscription]

            $services = @{};
            $_.service | ForEach-Object {
                $svc = New-Object PSCustomObject
                Add-Member -NotePropertyMembers @{
                    Kind = $_.kind;
                    Type = $_.type;
                    Name = $_.name;
                    ID = $_.InnerText;
                    Environment = $env;
                    Uri = $_.uri;
                    Datacenter = $_.dc;
                } -InputObject $svc
                $services[$svc.Name] = $svc
            };

            Add-Member -NotePropertyMembers @{
                Version = 3;
                Name = $_.name;
                Subscription = $sub;
                Protected = $_.protected -and ([String]::Equals($_.protected, "true", "OrdinalIgnoreCase"));
                Services = $services;
            } -InputObject $env
            $Environments[$_.name] = $env
        }
    }

    $ret = New-Object PSCustomObject
    Add-Member -InputObject $ret -NotePropertyMembers @{
        "Version"=3;
        "Environments"=$Environments;
        "Subscriptions"=$Subscriptions
    }
    $ret
}

function Get-V2Environments($NuGetOpsDefinition) {
    $Environments = @{};
    $Subscriptions = @{};

    if($NuGetOpsDefinition -and (Test-Path $NuGetOpsDefinition)) {
        $EnvironmentsList = Join-Path $NuGetOpsDefinition "Environments.xml"
        if(Test-Path $EnvironmentsList) {
            $x = [xml](cat $EnvironmentsList)
            $Environments = @{};
            $x.environments.environment | ForEach-Object {
                $Environments[$_.name] = New-Object PSCustomObject
                Add-Member -NotePropertyMembers @{
                    Version = $NuGetOpsVersion;
                    Name = $_.name;
                    Protected = $_.protected -and ([String]::Equals($_.protected, "true", "OrdinalIgnoreCase"));
                    Frontend = $_.frontend;
                    Backend = $_.backend;
                    Subscription = $_.subscription
                    Type = $_.type
                } -InputObject $Environments[$_.name]
            }
        } else {
            Write-Warning "Environments list not found at $EnvironmentsList. No Environments will be available."
        }

        $Subscriptions = Get-Subscriptions -NuGetOpsDefinition $NuGetOpsDefinition

        $Environments.Keys | foreach {
            $subName = $Environments[$_].Subscription
            if($Subscriptions[$subName] -ne $null) {
                $Environments[$_].Subscription = $Subscriptions[$subName];
            }
        }
    }

    $ret = New-Object PSCustomObject
    Add-Member -InputObject $ret -NotePropertyMembers @{
        "Version"=2;
        "Environments"=$Environments;
        "Subscriptions"=$Subscriptions
    }
    $ret
}