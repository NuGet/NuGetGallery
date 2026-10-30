// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IUndoSoftDeleteService
    {
        /// <summary>
        /// Gets recovery data matching the package's latest committed deletion, or null if it is not eligible.
        /// </summary>
        Task<PackageDeleteBackup> GetBackupAsync(Package package);

        /// <summary>
        /// Undoes the specified committed deletion. Repeating a completed undo makes no changes.
        /// </summary>
        Task UndoAsync(Package package, int packageDeleteKey, string reason, string callerIdentity = null);
    }
}