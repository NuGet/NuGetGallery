
using System.Collections.Generic;
namespace NuGetGallery
{
    public class Role
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}