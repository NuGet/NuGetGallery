using System.Collections.Generic;

namespace NuGetGallery
{
    public class PackageRegistration : IEntity
    {
        public PackageRegistration()
        {
            Owners = new HashSet<User>();
            Packages = new HashSet<Package>();
        }

        public int Key { get; set; }

        public string Id { get; set; }
        public int DownloadCount { get; set; }
        public virtual ICollection<User> Owners { get; set; }
        public virtual ICollection<Package> Packages { get; set; }
    }
}