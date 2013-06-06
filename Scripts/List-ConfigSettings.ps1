param([string]$AssemblyPath)
$AssemblyPath = Resolve-Path $AssemblyPath
$asm = [System.Reflection.Assembly]::LoadFrom($AssemblyPath)
$typ = $asm.GetType("NuGetGallery.Configuration.AppConfiguration");
$typ.GetProperties() | foreach {
    $name = $_.Name;
    $attr = @($_.GetCustomAttributes([System.ComponentModel.DisplayNameAttribute], $true));
    if($attr.Length -gt 0) {
        $name = $attr[0].DisplayName;
    }

    "<Setting name=`"Gallery.$name`" />"
}