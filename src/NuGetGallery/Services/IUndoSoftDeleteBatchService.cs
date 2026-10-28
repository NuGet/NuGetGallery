// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IUndoSoftDeleteBatchService
    {
        Task<IReadOnlyList<PackageDeleteBackup>> PreviewAsync(IReadOnlyList<UndoPackageRequest> requests);

        Task<UndoSoftDeleteBatchResult> UndoAsync(
            IReadOnlyList<UndoPackageRequest> requests, string reason, string callerIdentity);
    }
}