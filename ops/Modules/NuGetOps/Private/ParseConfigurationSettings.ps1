function ParseConfigurationSettings($file) {
	$xml = [xml](cat $file)
    $role = $xml.ServiceConfiguration.Role | Select-Object -First 1

    $hash = @{}
    $role.ConfigurationSettings.Setting | ForEach-Object {
        $hash[$_.name] = $_.value
    }
    $hash
}