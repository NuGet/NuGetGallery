// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Services.Gallery;
using NuGet.Services.Gallery.Entities;

namespace NuGetGallery.Areas.Admin.Models
{
    public partial class Admin : IEntity
    {
        [Key]
        public int Key { get; set; }

        [Required(ErrorMessage ="Please provide a Pagerduty username")]
        [StringLength(255)]
        [Display(Name = "Pager Duty username")]
        [Index]
        public string PagerDutyUsername { get; set; }

        [Required(ErrorMessage = "Please provide a NuGet Gallery username")]
        [Display(Name = "NuGet Gallery username")]
        [StringLength(255)]
        [Index]
        public string GalleryUsername { get; set; }

        public bool AccessDisabled { get; set; }

        // Navigation properties
        public virtual ICollection<Issue> Issues { get; set; }
        public virtual ICollection<History> HistoryEntries { get; set; }
    }
}
