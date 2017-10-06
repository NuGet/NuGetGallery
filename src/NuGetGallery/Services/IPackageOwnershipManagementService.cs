// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IPackageOwnershipManagementService
    {
        /// <summary>
        /// Add the user as an owner to the package. Also add the package registration
        /// to the reserved namespaces owned by this user if the Id matches any of the 
        /// reserved prefixes. Also mark the package registration as verified if it matches any
        /// of the user owned reserved namespaces.
        /// </summary>
        /// <param name="packageRegistration">The package registration that is intended to get ownership.</param>
        /// <param name="user">The user to add as an owner to the package.</param>
        Task AddPackageOwnerAsync(PackageRegistration packageRegistration, User user);

        /// <summary>
        /// Remove the user as from the list of owners of the package. Also remove the package registration
        /// from the reserved namespaces owned by this user if the Id matches any of the reserved prefixes
        /// and the user is the only package owner that owns the namespace that matches the package registration.
        /// </summary>
        /// <param name="packageRegistration">The package registration that is intended to get ownership.</param>
        /// <param name="user">The user to add as an owner to the package.</param>
        Task RemovePackageOwnerAsync(PackageRegistration packageRegistration, User user);
    }
}