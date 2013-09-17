using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace NuGetGallery
{
    public class Credential : IEntity
    {
        public int Key { get; set; }
        public int UserKey { get; set; }

        [StringLength(maximumLength: 64)]
        public string Type { get; set; }
        
        [StringLength(maximumLength: 256)]
        public string Identifier { get; set; }
        
        [StringLength(maximumLength: 256)]
        public string Value { get; set; }

        public virtual User User { get; set; }
    }
}
