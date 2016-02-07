// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

        [Required(ErrorMessage ="Please enter the Pagerduty User Name aka Microsoft alias")]
        [StringLength(255)]
        [Display(Name = "Pager Duty UserName")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Please enter the NuGet Gallery User Name")]
        [Display(Name = "NuGet Gallery UserName")]
        [StringLength(255)]
        public string GalleryUserName { get; set; }

        public int AdminStatus { get; set; }
    }
}
