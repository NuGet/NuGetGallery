// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

namespace NuGetGallery.Services.Models
{
    public enum PackageDeleteDecision
    {
        /// <summary>
        /// The user would like to delete the package.
        /// </summary>
        [Description("Delete this package")]
        DeletePackage,

        /// <summary>
        /// The user would like to contact support instead of deleting the package.
        /// </summary>
        [Description("Contact support")]
        ContactSupport,
    }
}