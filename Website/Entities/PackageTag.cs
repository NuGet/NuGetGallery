using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Entities
{
    public class PackageTag : IEntity
    {
        public Tag Tag { get; set; }
        public int TagKey { get; set; }

        public PackageRegistration Package { get; set; }
        public int PackageRegistrationKey { get; set; }
    }
}