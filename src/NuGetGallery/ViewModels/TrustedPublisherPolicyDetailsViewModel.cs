// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    [DebuggerDisplay("{Name,nq}")]
    public abstract class TrustedPublisherPolicyDetailsViewModel
    {
        protected TrustedPublisherPolicyDetailsViewModel() { }

        /// <summary>
        /// Publisher type.
        /// </summary>
        public abstract FederatedCredentialType PublisherType { get; }
    }
}
