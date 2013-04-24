$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

$Output = "$Root\..\Packages"
if(Test-Path $Output) {
    del -rec -for $Output
}
mkdir $Output
$Output = Resolve-Path $Output

dir "$Root\*.nuspec" | foreach {
    & nuget pack $_.FullName -OutputDirectory $Output
}