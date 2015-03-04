using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGet.Services.Publish
{
    public class PublicationDetails
    {
        public DateTime Published { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public PublicationVisibility Visibility { get; set; }
    }
}