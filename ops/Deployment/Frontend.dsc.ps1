Configuration Frontend {
    param(
        [Parameter(Mandatory=$true, Position=0)][string]$MachineName)

    Node $MachineName {
        # Install IIS
        WindowsFeature IIS {
            Ensure = "Present"
            Name = "Web-Server"
        }

        # Install ASP.Net 4.5
        WindowsFeature ASPNet45 {
            Ensure = "Present"
            Name = "Web-Asp-Net45"
        }
    }
}