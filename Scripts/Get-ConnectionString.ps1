function Get-ConnectionString($configPath, $connectionStringName) 
{
  $config = [xml](get-content $configPath)
  
  $connectionString = ($config.configuration.connectionStrings.add | where { $_.name -eq $connectionStringName }).connectionString
  
  $connectionString = $connectionString.Replace("=", "%3D")
  $connectionString = $connectionString.Replace(";", "%3B")

  return $connectionString
}
