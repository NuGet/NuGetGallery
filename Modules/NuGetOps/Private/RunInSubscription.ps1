function RunInSubscription($name, [scriptblock]$scriptblock) {
    $oldSub = Get-AzureSubscription | Where-Object { $_.IsDefault }
    if(!$oldSub -or ($oldSub.SubscriptionName -ne $name)) {
        Select-AzureSubscription $name
    }
    $scriptblock.Invoke();
    if($oldSub -and ($oldSub.SubscriptionName -ne $name)) {
        Select-AzureSubscription $oldSub.SubscriptionName
    }
}