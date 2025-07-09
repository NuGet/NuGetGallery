// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;

namespace NuGetGallery
{
    /// <summary>
    /// Base class for policy details.
    /// </summary>
    [DebuggerDisplay("{PolicyName,nq}")]
    public sealed class TrustedPublisherPolicyViewModel
    {
        public int Key { get; set; }

        /// <summary>
        /// User provided policy name.
        /// </summary>
        public string PolicyName { get; set; }

        /// <summary>
        /// NuGet package owner.
        /// </summary>
        public string Owner { get; set; }

        public TrustedPublisherPolicyInvalidReason? InvalidReason { get; set; }

        public TrustedPublisherPolicyDetailsViewModel PolicyDetails { get; set; }

        public string PublisherName => PolicyDetails?.PublisherType.ToString() ?? string.Empty;
    }
}
