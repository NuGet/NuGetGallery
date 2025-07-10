// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace NuGetGallery
{
    /// <summary>
    /// Base class for policy details.
    /// </summary>
    [DebuggerDisplay("{PolicyName,nq}")]
    public sealed class TrustedPublisherPolicyViewModel
    {
        private string _policyName = string.Empty;
        private string _owner = string.Empty;

        public int Key { get; set; }

        /// <summary>
        /// User provided policy name.
        /// </summary>
        [Required]
        public string PolicyName
        {
            get => _policyName;
            set => _policyName = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// NuGet package owner.
        /// </summary>
        [Required]
        public string Owner
        {
            get => _owner;
            set => _owner = value?.Trim() ?? string.Empty;
        }

        public TrustedPublisherPolicyInvalidReason? InvalidReason { get; set; }

        public TrustedPublisherPolicyDetailsViewModel PolicyDetails { get; set; }

        public string PublisherName => PolicyDetails?.PublisherType.ToString() ?? string.Empty;
    }
}
