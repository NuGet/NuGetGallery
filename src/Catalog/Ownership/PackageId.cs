using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class PackageId : RegistrationId
    {
        public NuGetVersion Version { get; set; }

        public string PackageRelativeAddress { get { return RegistrationRelativeAddress + "#" + Version.ToNormalizedString(); } }
    }
}