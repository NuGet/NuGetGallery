// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery
{
    public class TrustedPublisherListViewModel
    {
        public string Username { get; set; }
        public IList<TrustedPublisherViewModel> TrustedPublishers { get; set; }
        public IList<ApiKeyOwnerViewModel> PackageOwners { get; set; }
        public bool IsDeprecationApiEnabled { get; set; }
    }}
