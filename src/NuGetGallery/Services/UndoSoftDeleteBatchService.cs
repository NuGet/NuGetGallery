// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class UndoSoftDeleteBatchService : IUndoSoftDeleteBatchService
    {
        private readonly ILifetimeScope _scope;

        public UndoSoftDeleteBatchService(ILifetimeScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public async Task<UndoSoftDeleteBatchResult> UndoAsync(
            IReadOnlyList<UndoPackageRequest> requests, string reason, string callerIdentity)
        {
            var selection = NormalizeRequests(requests);
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            {
                throw new UserSafeException("An undo reason of 1000 characters or fewer is required.");
            }

            var results = new List<UndoSoftDeletePackageResult>();
            foreach (var request in selection)
            {
                try
                {
                    // Use a fresh database context per package; rollback does not reset EF's tracked changes.
                    using (var scope = _scope.BeginLifetimeScope())
                    {
                        var package = FindDeletedPackage(scope.Resolve<IPackageService>(), request);
                        await scope.Resolve<IUndoSoftDeleteService>().UndoAsync(package, reason, callerIdentity);
                    }
                    results.Add(new UndoSoftDeletePackageResult(request));
                }
                catch (Exception exception)
                {
                    results.Add(new UndoSoftDeletePackageResult(request, exception));
                }
            }

            return new UndoSoftDeleteBatchResult(results);
        }

        private static Package FindDeletedPackage(IPackageService packageService, UndoPackageRequest request)
        {
            var package = packageService.FindPackageByIdAndVersionStrict(request.Id, request.Version);
            if (package == null)
            {
                throw new UserSafeException($"{request.Id} {request.Version} was not found. Refresh the selection.");
            }
            if (package.PackageStatusKey != PackageStatus.Deleted)
            {
                throw new UserSafeException($"{request.Id} {request.Version} is not soft deleted. Refresh the selection.");
            }
            return package;
        }

        private static List<UndoPackageRequest> NormalizeRequests(IReadOnlyList<UndoPackageRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                throw new UserSafeException("Select at least one deleted package to undo soft deletion.");
            }

            var selection = new List<UndoPackageRequest>();
            var identities = new HashSet<PackageIdentity>();
            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Version))
                {
                    throw new UserSafeException("Each selected package must include an ID and version.");
                }

                if (!NuGetVersion.TryParse(request.Version.Trim(), out var version))
                {
                    throw new UserSafeException("Each selected package must include a valid NuGet version.");
                }
                var identity = new PackageIdentity(request.Id.Trim(), version);
                if (!identities.Add(identity))
                {
                    throw new UserSafeException("A package version cannot be selected more than once.");
                }

                selection.Add(new UndoPackageRequest
                {
                    Id = identity.Id,
                    Version = identity.Version.ToNormalizedString()
                });
            }
            return selection;
        }
    }
}