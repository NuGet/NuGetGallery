$groups = 
    dir -recurse -filter packages.config | 
    foreach { 
        $x = [xml](cat $_.FullName); 
        $project = $_.DirectoryName;
        $x.packages.package | select @{"Name"="project";"Expression"={$project}},id,version 
    } | 
    group id

Write-Host "Package Versions:"
$groups | where { @($_.Group | select -unique id,version).Count -eq 1 } | foreach { $_.Group[0] | select id,version }

$duped = $groups | where { @($_.Group | select -unique id,version).Count -gt 1 } | foreach { $_.Group | group version };
if($duped) {
    Write-Warning "Packages with Duplicate Versions: "
    $duped
} else {
    Write-Host
    Write-Host -ForegroundColor Green "No dupes!"
}