using System;

namespace NuGet.Services.Metadata.Catalog.Ownership
{
    public class OwnershipOwner
    {
        public string ObjectId { get; set; }

        public Uri GetUri(Uri baseAddress)
        {
            string fragment = string.Format("#owner/{0}", ObjectId);
            return new Uri(baseAddress, fragment);
        }
    }
}
