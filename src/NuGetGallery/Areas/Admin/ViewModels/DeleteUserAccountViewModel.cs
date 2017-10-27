// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class DeleteUserAccountViewModel
    {
        public DeleteUserAccountViewModel()
        {
            Unlist = true;
        }

        public List<ListPackageItemViewModel> Packages { get; set; }

        public User User { get; set; }

        public string AccountName { get; set; }

        [Required(ErrorMessage = "Please sign using your name.")]
        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }

        [Display(Name = "Unlist the packages without an user.")]
        public bool Unlist { get; set; }

        public bool HasOrphanPackages { get; set; }
    }

}
