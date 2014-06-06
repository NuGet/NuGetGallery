using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    public class Feed : IEntity
    {
        public Feed()
        {
            Managers = new HashSet<User>();
            Packages = new HashSet<FeedPackage>();
            Rules = new HashSet<FeedRule>();
        }

        [StringLength(128)]
        public string Name { get; set; }
        public bool Inclusive { get; set; }

        public virtual ICollection<User> Managers { get; set; }
        public virtual ICollection<FeedPackage> Packages { get; set; }
        public virtual ICollection<FeedRule> Rules { get; set; }

        public int Key { get; set; }
    }
}
