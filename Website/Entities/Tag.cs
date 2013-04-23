using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace NuGetGallery.Entities
{
    public class Tag : IEntity
    {
        int Key { get; set; }

        [StringLength(64)]
        string Text { get; set; }
    }
}