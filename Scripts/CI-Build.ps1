$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

#Set up env
if ((test-path "C:\Scripts\Set-PreviewVars.ps1") -eq $false) {
    print-error("=================================================")
    print-error("               Env could not be set              ")
    print-error("=================================================")
} 

C:\Scripts\Set-PreviewVars.ps1

#Do Work Brah

& "$ScriptRoot\Package.ps1"
if($LastExitCode -ne 0) {
    print-error("=================================================")
    print-error("        Error creating the Azure Packages.       ")
    print-error("=================================================")
} else {
    & "$ScriptRoot\Deploy.ps1"
    if($LastExitCode -ne 0) {
    	print-error("=================================================")
        print-error("        Error deploying the Azure Packages.      ")
        print-error("=================================================")
    } else {
        print-success("=================================================")
        print-success("                All Done, Son.                   ")
        print-success("=================================================")
    }
}
