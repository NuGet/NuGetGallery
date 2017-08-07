// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// A service that stores <see cref="PackageDeletionRecord"/>s.
    /// </summary>
    public interface IPackageDeletionRecordService
    {
        /// <summary>
        /// Saves a <see cref="PackageDeletionRecord"/> for a <see cref="Package"/> that is being deleted.
        /// </summary>
        /// <param name="package">The <see cref="Package"/> that is being deleted.</param>
        Task SaveDeletionRecord(Package package);
    }
}