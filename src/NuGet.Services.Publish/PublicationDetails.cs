using NuGet.Services.Metadata.Catalog.Ownership;
using System;

namespace NuGet.Services.Publish
{
    public class PublicationDetails
    {
        public DateTime Published { get; set; }
        public OwnershipOwner Owner { get; set; }
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public PublicationVisibility Visibility { get; set; }
    }
}