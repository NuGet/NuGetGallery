
namespace NuGet.Services.Publish
{
    public class RegistrationId
    {
        public string Domain { get; set; }
        public string Id { get; set; }

        public string RelativeAddress { get { return Domain.ToLowerInvariant() + "/" + Id.ToLowerInvariant(); } }
    }
}