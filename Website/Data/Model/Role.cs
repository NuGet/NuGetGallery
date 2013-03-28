using System.Collections.Generic;

namespace NuGetGallery.Data.Model
{
    public class Role : IEntity
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}