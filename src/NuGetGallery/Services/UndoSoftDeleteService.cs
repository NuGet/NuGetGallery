// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class UndoSoftDeleteService : IUndoSoftDeleteService
    {
        private readonly IEntityRepository<PackageDelete> _packageDeletes;
        private readonly IPackageDeleteBackupService _backups;

        public UndoSoftDeleteService(
            IEntityRepository<PackageDelete> packageDeletes,
            IPackageDeleteBackupService backups)
        {
            _packageDeletes = packageDeletes ?? throw new ArgumentNullException(nameof(packageDeletes));
            _backups = backups ?? throw new ArgumentNullException(nameof(backups));
        }

        public async Task<PackageDeleteBackup> GetBackupAsync(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.Key <= 0 || package.PackageStatusKey != PackageStatus.Deleted || package.PackageRegistration == null)
            {
                return null;
            }

            // Recovery JSON alone is not proof of a committed deletion. Failed attempts also retain JSON.
            var deletion = _packageDeletes.GetAll()
                .Where(d => d.Packages.Any(p => p.Key == package.Key))
                .OrderByDescending(d => d.Key)
                .FirstOrDefault();
            if (deletion == null)
            {
                return null;
            }

            var backup = await _backups.GetAsync(deletion.Key, package.Key);
            if (backup == null
                || backup.PackageDeleteKey != deletion.Key
                || backup.PackageKey != package.Key
                || backup.DeletedOnUtc != deletion.DeletedOn
                || !StringComparer.OrdinalIgnoreCase.Equals(backup.PackageId, package.PackageRegistration.Id)
                || !StringComparer.OrdinalIgnoreCase.Equals(backup.NormalizedVersion, package.NormalizedVersion))
            {
                return null;
            }

            if (backup.Symbols.Any(saved => !package.SymbolPackages.Any(symbol =>
                symbol.Key == saved.SymbolPackageKey && symbol.StatusKey == PackageStatus.Deleted)))
            {
                return null;
            }

            return backup;
        }
    }
}