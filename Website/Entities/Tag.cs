using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    public class Tag : IEntity
    {
        [Key]
        public int Key { get; set; }

        [StringLength(64)]
        [Required]
        public string Name { get; set; }

        [StringLength(1024)]
        public string Description { get; set; }
    }
}