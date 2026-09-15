// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the form for creating an owner-scoped staging group.
    /// </summary>
    public class CreateStagingGroupViewModel
    {
        [Required]
        [Display(Name = "Owner")]
        public string Owner { get; set; }

        [Required]
        [StringLength(StagingGroupIdValidation.MaximumLength)]
        [RegularExpression(StagingGroupIdValidation.Pattern, ErrorMessage = "Group IDs must start and end with a letter or number and may contain periods, underscores, and hyphens.")]
        [Display(Name = "Group ID")]
        public string Id { get; set; }

        private string _name;

        [StringLength(128)]
        [Display(Name = "Display name")]
        public string Name
        {
            get => _name;
            set => _name = value?.Trim();
        }

        public IReadOnlyList<string> Owners { get; set; }
    }
}
