

class PackageDefinition(object):

    NUGET_EXTENSION = ".nupkg"

    def __init__(self):
        self.packageId = ''
        self.packageVersion = ''

    def __init__(self, packageId, packageVersion):
        self.packageId = packageId.strip()
        self.packageVersion = packageVersion.strip()

    def fromRequestUrl(requestUrl):
        # requestUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration/2.2.0/microsoft.extensions.configuration.2.2.0.nupkg"
        # packageId = "microsoft.extensions.configuration"
        # packageVersion = "2.2.0"
        # Split the requestUrl by '/'
        # Extract the packageId and packageVersion
        # return PackageDefinition(packageId, packageVersion)
        pass


