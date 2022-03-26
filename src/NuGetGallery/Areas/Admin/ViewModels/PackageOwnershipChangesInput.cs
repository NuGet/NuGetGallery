// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageOwnershipChangesInput
    {
        [Required(ErrorMessage = "You must provide at least one package ID.")]
        public string PackageIds { get; set; }

        [Required(ErrorMessage = "You must provide a requestor username.")]
        public string Requestor { get; set; }

        public string AddOwners { get; set; }

        public string RemoveOwners { get; set; }

        public string Message { get; set; }

        public bool SkipRequestFlow { get; set; }
    }
}