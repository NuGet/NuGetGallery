// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuGetGallery
{
    [DebuggerDisplay("{Username,nq}, policies={Policies.Count}")]
    public class TrustedPublisherPolicyListViewModel
    {
        public string Username { get; set; }
        public IReadOnlyList<string> PackageOwners { get; set; } = Array.Empty<string>();
        public IReadOnlyList<TrustedPublisherPolicyViewModel> Policies { get; set; } = Array.Empty<TrustedPublisherPolicyViewModel>();
    }
}
