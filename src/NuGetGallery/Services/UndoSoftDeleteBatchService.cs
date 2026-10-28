// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class UndoSoftDeleteBatchService : IUndoSoftDeleteBatchService
    {
        private readonly IPackageService _packageService;
        private readonly IUndoSoftDeleteService _undoSoftDeleteService;

        public UndoSoftDeleteBatchService(IPackageService packageService, IUndoSoftDeleteService undoSoftDeleteService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _undoSoftDeleteService = undoSoftDeleteService ?? throw new ArgumentNullException(nameof(undoSoftDeleteService));
        }

        public async Task<IReadOnlyList<PackageDeleteBackup>> PreviewAsync(IReadOnlyList<UndoPackageRequest> requests)
        {
            var selection = await PreflightAsync(requests, validateDeletionKeys: false);
            return selection.Select(item => item.Backup).ToList();
        }

        public async Task<UndoSoftDeleteBatchResult> UndoAsync(
            IReadOnlyList<UndoPackageRequest> requests, string reason, string callerIdentity)
        {
            // Recheck the entire selection before restoring any package. Never substitute a newer deletion.
            var selection = await PreflightAsync(requests, validateDeletionKeys: true);

            foreach (var item in selection)
            {
                try
                {
                    await _undoSoftDeleteService.UndoAsync(
                        item.Package, item.Backup.PackageDeleteKey, reason, callerIdentity);
                }
                catch (Exception exception)
                {
                    // Do not reuse the request's entity context after a failed restore or reflow.
                    return new UndoSoftDeleteBatchResult(item.Request, exception);
                }
            }

            return new UndoSoftDeleteBatchResult();
        }

        private async Task<List<(UndoPackageRequest Request, Package Package, PackageDeleteBackup Backup)>> PreflightAsync(
            IReadOnlyList<UndoPackageRequest> requests, bool validateDeletionKeys)
        {
            if (requests == null || requests.Count == 0)
            {
                throw new UserSafeException("Select at least one deleted package to undo soft deletion.");
            }

            var selection = new List<(UndoPackageRequest Request, Package Package, PackageDeleteBackup Backup)>();
            var keys = new HashSet<int>();
            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Version))
                {
                    throw new UserSafeException("Each selected package must include an ID and version.");
                }

                var package = _packageService.FindPackageByIdAndVersionStrict(request.Id, request.Version);
                if (package == null)
                {
                    throw new UserSafeException($"{request.Id} {request.Version} was not found. Refresh the selection.");
                }
                if (!keys.Add(package.Key))
                {
                    throw new UserSafeException("A package version cannot be selected more than once.");
                }

                var backup = await _undoSoftDeleteService.GetBackupAsync(package);
                if (backup == null)
                {
                    throw new UserSafeException($"{package.PackageRegistration.Id} {package.NormalizedVersion} has no matching recovery data for its latest committed deletion. Undo soft delete is unavailable.");
                }
                if (validateDeletionKeys && request.PackageDeleteKey != backup.PackageDeleteKey)
                {
                    throw new UserSafeException("The selected deletion changed. Refresh the selection before undoing soft deletion.");
                }

                selection.Add((request, package, backup));
            }
            return selection;
        }
    }
}