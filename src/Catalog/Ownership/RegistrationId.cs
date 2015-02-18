
namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class RegistrationId
    {
        public string Domain { get; set; }
        public string Id { get; set; }

        public string RegistrationRelativeAddress { get { return Domain.ToLowerInvariant() + "/" + Id.ToLowerInvariant(); } }
    }
}