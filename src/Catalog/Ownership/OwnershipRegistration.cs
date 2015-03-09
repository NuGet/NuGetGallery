using System;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipRegistration
    {
        public string Id { get; set; }
        public string Namespace { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string ns = string.IsNullOrEmpty(Namespace) ? "nuget.org" : Namespace;
            string fragment = string.Format("#registration/{0}/{1}", ns, Id);
            return new Uri(baseAddress, fragment);
        }
    }
}
