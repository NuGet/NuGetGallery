// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class OwnerRequestsViewModel
    {
        /// <summary>
        /// A model to show the requests for this user to become an owner of a package.
        /// </summary>
        public OwnerRequestsListViewModel Received { get; set; }

        /// <summary>
        /// A model to show the requests this user has sent to other users to become owners.
        /// </summary>
        public OwnerRequestsListViewModel Sent { get; set; }
    }
}