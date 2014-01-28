function GetDeployment($Service, $Slot = "production") {
    if(!$Service.ID -or !$Service.Environment) {
        throw "Invalid Service object provided"
    }

    if(!$Service.Environment.Subscription) {
        throw "No Subscription is available for this environment. Do you have access?"
    }

    if($Service.Type -eq "Website") {
        if($Slot -ne "production") {
            Write-Warning "Websites do not support multiple slots, ignoring -Slot parameter"
        }
        RunInSubscription $Service.Environment.Subscription.Name {
            Get-AzureWebsite -Name $Service.ID
        }
    } elseif($Service.Type -eq "CloudService") {
        RunInSubscription $Service.Environment.Subscription.Name {
            Get-AzureDeployment -ServiceName $Service.ID -Slot $Slot
        }
    }
}