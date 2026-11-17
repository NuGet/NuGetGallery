// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// Represents the form for renaming a staging group.
    /// </summary>
    public class RenameStagingGroupViewModel
    {
        private string _name;

        /// <summary>
        /// Gets or sets the group display name.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [StringLength(128)]
        [Display(Name = "Display name")]
        public string Name
        {
            get => _name;
            set => _name = value?.Trim();
        }
    }
}
