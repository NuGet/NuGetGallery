// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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
        [StringLength(StagingGroupIdValidation.MaximumLength)]
        [RegularExpression(StagingGroupIdValidation.Pattern)]
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

    internal static class StagingGroupIdValidation
    {
        public const int MaximumLength = 64;
        public const string Pattern = @"^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$";

        public static bool IsValid(string groupId)
        {
            return groupId != null && groupId.Length <= MaximumLength && Regex.IsMatch(groupId, Pattern);
        }
    }
}
