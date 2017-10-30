// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public enum PackageAction
    {
        /// <summary>
        /// The ability to view hidden package versions or metadata.
        /// </summary>
        DisplayPrivatePackage,

        /// <summary>
        /// The ability to upload new versions of an existing package ID.
        /// </summary>
        UploadNewVersion,

        /// <summary>
        /// The ability to edit an existing package version.
        /// </summary>
        Edit,

        /// <summary>
        /// The ability to unlist or relist an existing package version.
        /// </summary>
        Unlist,

        /// <summary>
        /// The ability to add or remove owners of the package.
        /// </summary>
        ManagePackageOwners,

        /// <summary>
        /// The ability to report a package as its owner.
        /// </summary>
        ReportMyPackage,
    }
}