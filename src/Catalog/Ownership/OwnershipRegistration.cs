using System;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipRegistration
    {
        public string Id { get; set; }
        public string Prefix { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string prefix = string.IsNullOrEmpty(Prefix) ? "nuget.org" : Prefix;
            string fragment = string.Format("#registration/{0}/{1}", prefix, Id);
            return new Uri(baseAddress, fragment);
        }
    }
}
