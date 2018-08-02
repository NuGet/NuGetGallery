// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class DeleteAccountAsAdminViewModel
    {
        public DeleteAccountAsAdminViewModel()
        {
            ShouldUnlist = true;
        }

        public DeleteAccountAsAdminViewModel(IDeleteAccountViewModel model)
        {
            AccountName = model.AccountName;
            HasOrphanPackages = model.HasOrphanPackages;
        }

        public string AccountName { get; set; }

        [Required(ErrorMessage = "Please sign using your name.")]
        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }

        [Display(Name = "Unlist any orphaned packages.")]
        public bool ShouldUnlist { get; set; }

        public bool HasOrphanPackages { get; set; }
    }
}
