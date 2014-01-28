function EnsureService($Service) {
    if($Service -is [string]) {
        if(!$CurrentEnvironment) {
            throw "This command requires an environment"
        }
        $Service = Get-NuGetService $Service -ForceSingle
    }
    $Service
}