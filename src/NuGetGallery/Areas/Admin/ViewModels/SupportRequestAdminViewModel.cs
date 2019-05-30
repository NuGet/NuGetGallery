// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class SupportRequestAdminViewModel
    {
        public SupportRequestAdminViewModel()
        {
        }

        public SupportRequestAdminViewModel(NuGetGallery.Admin a)
        {
            Key = a.Key;
            GalleryUsername = a.GalleryUsername;
            AccessDisabled = a.AccessDisabled;
        }

        public int Key { get; set; }

        [Required(ErrorMessage = "Please provide a NuGet Gallery username")]
        [Display(Name = "NuGet Gallery username")]
        [StringLength(255)]
        [Index]
        public string GalleryUsername { get; set; }

        public bool AccessDisabled { get; set; }
    }
}