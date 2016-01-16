namespace NuGetGallery.Areas.Admin.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class Admin : IEntity
    {
        [Key]
        public int Key { get; set; }

        [StringLength(255)]
        public string UserName { get; set; }
    }
}
