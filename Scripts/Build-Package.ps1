$ScriptRoot = (Split-Path -parent $MyInvocation.MyCommand.Definition)
. $ScriptRoot\_Common.ps1

#Do Work Brah
 & "$ScriptRoot\Build.ps1"
if($LastExitCode -ne 0) {
    print-error("=================================================")
    print-error("            Error building project.              ")
    print-error("=================================================")
} else {
    & "$ScriptRoot\Package.ps1"
    if($LastExitCode -ne 0) {
        print-error("=================================================")
        print-error("        Error creating the Azure Packages.       ")
        print-error("=================================================")
    } else {
        print-success("=================================================")
        print-success("                All Done, Son.                   ")
        print-success("=================================================")
    }
}
