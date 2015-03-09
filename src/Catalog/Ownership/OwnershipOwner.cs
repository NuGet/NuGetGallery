using System;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipOwner
    {
        public string NameIdentifier { get; set; }
        public string Name { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string Email { get; set; }
        public string Iss { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string fragment = string.Format("#owner/{0}", NameIdentifier);
            return new Uri(baseAddress, fragment);
        }
    }
}
