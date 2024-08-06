// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Entities;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class PackageRegistrationOwnershipChangeModel
    {
        public PackageRegistrationOwnershipChangeModel(
            PackageRegistration packageRegistration,
            bool requestorHasPermissions,
            IReadOnlyDictionary<string, PackageRegistrationUserChangeModel> usernameToState)
        {
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
            RequestorHasPermissions = requestorHasPermissions;
            UsernameToState = usernameToState ?? throw new ArgumentNullException(nameof(usernameToState));
        }

        /// <summary>
        /// The package ID this ownership change relates to.
        /// </summary>
        public string Id => PackageRegistration.Id;

        /// <summary>
        /// The package registration this ownership change relates to.
        /// </summary>
        public PackageRegistration PackageRegistration { get; }

        /// <summary>
        /// Whether or not the request has permissions to make ownership changes on this package.
        /// </summary>
        public bool RequestorHasPermissions { get; }

        /// <summary>
        /// The state of all current, added, and removed owners on this package registration.
        /// </summary>
        public IReadOnlyDictionary<string, PackageRegistrationUserChangeModel> UsernameToState { get; }
    }
}