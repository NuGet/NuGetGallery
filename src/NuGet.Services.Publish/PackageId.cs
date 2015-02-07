using NuGet.Versioning;

namespace NuGet.Services.Publish
{
    public class PackageId : RegistrationId
    {
        public NuGetVersion Version { get; set; }

        public new string RelativeAddress { get { return base.RelativeAddress + "#" + Version.ToNormalizedString(); } }
    }
}