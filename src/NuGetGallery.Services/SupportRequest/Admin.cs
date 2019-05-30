// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public partial class Admin : IEntity
    {
        [Key]
        public int Key { get; set; }

        [Required(ErrorMessage = "Please provide a NuGet Gallery username")]
        [Display(Name = "NuGet Gallery username")]
        [StringLength(255)]
        [Index]
        public string GalleryUsername { get; set; }

        public bool AccessDisabled { get; set; }

        public virtual ICollection<Issue> Issues { get; set; }

        public virtual ICollection<History> HistoryEntries { get; set; }
    }
}
