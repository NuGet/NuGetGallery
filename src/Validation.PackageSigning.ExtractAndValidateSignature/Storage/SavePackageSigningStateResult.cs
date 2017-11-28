// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.PackageSigning.Storage
{
    public enum SavePackageSigningStateResult
    {
        /// <summary>
        /// Successfully persisted the <see cref="PackageSigningStatus"/>.
        /// </summary>
        Success,

        /// <summary>
        /// Failed to persist the <see cref="PackageSigningStatus"/> as a status already
        /// exists with the same validation id.
        /// </summary>
        StatusAlreadyExists,
    }
}
