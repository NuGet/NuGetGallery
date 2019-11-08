// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery.Areas.Admin.Models
{
    public class RevokeApiKeysRequest
    {
        public List<string> SelectedApiKeyRevokeViewModelsInJSON { get; set; }

        [Required(ErrorMessage = "Please sign using your name.")]
        [StringLength(1000)]
        [Display(Name = "Signature")]
        public string Signature { get; set; }
    }
}