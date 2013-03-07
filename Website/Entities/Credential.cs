using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class Credential : IEntity
    {
        public int Key { get; set; }
        public int UserKey { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public virtual User User { get; set; }
    }
}
