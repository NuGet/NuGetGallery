// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGetGallery
{
    /// <summary>
    /// Describes a request to create a staging group.
    /// </summary>
    public class CreateStagingGroupRequest
    {
        private string _name;

        /// <summary>
        /// Gets or sets the immutable owner-scoped group ID.
        /// </summary>
        [Required]
        [StringLength(64)]
        [RegularExpression(@"^[A-Za-z0-9._-]+$")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the group display name.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [StringLength(128)]
        public string Name
        {
            get => _name;
            set => _name = value?.Trim();
        }
    }
}
