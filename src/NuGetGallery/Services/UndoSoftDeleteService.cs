// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class UndoSoftDeleteService : IUndoSoftDeleteService
    {
        private readonly IEntityRepository<PackageDelete> _packageDeletes;
        private readonly IPackageDeleteBackupService _backups;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageService _packageService;
        private readonly ISymbolPackageService _symbolPackageService;
        private readonly IPackageFileService _packageFiles;
        private readonly ISymbolPackageFileService _symbolFiles;
        private readonly ICoreLicenseFileService _licenses;
        private readonly ICoreReadmeFileService _readmes;
        private readonly IPackageUpdateService _packageUpdates;
        private readonly IValidationService _validation;
        private readonly IReflowPackageService _reflow;
        private readonly IAuditingService _auditing;
        private readonly IAppConfiguration _configuration;
        private readonly IDiagnosticsSource _diagnostics;

        public UndoSoftDeleteService(
            IEntityRepository<PackageDelete> packageDeletes,
            IPackageDeleteBackupService backups,
            IEntitiesContext entitiesContext,
            IPackageService packageService,
            ISymbolPackageService symbolPackageService,
            IPackageFileService packageFiles,
            ISymbolPackageFileService symbolFiles,
            ICoreLicenseFileService licenses,
            ICoreReadmeFileService readmes,
            IPackageUpdateService packageUpdates,
            IValidationService validation,
            IReflowPackageService reflow,
            IAuditingService auditing,
            IAppConfiguration configuration,
            IDiagnosticsService diagnostics)
        {
            _packageDeletes = packageDeletes ?? throw new ArgumentNullException(nameof(packageDeletes));
            _backups = backups ?? throw new ArgumentNullException(nameof(backups));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _symbolPackageService = symbolPackageService ?? throw new ArgumentNullException(nameof(symbolPackageService));
            _packageFiles = packageFiles ?? throw new ArgumentNullException(nameof(packageFiles));
            _symbolFiles = symbolFiles ?? throw new ArgumentNullException(nameof(symbolFiles));
            _licenses = licenses ?? throw new ArgumentNullException(nameof(licenses));
            _readmes = readmes ?? throw new ArgumentNullException(nameof(readmes));
            _packageUpdates = packageUpdates ?? throw new ArgumentNullException(nameof(packageUpdates));
            _validation = validation ?? throw new ArgumentNullException(nameof(validation));
            _reflow = reflow ?? throw new ArgumentNullException(nameof(reflow));
            _auditing = auditing ?? throw new ArgumentNullException(nameof(auditing));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _diagnostics = diagnostics?.SafeGetSource(nameof(UndoSoftDeleteService)) ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public async Task<PackageDeleteBackup> GetBackupAsync(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageStatusKey != PackageStatus.Deleted)
            {
                return null;
            }

            return await ReadBackupAsync(package);
        }

        private async Task<PackageDeleteBackup> ReadBackupAsync(Package package)
        {
            if (package.Key <= 0 || package.PackageRegistration == null)
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
                || backup.DeletedOnUtc != deletion.DeletedOn
                || !StringComparer.OrdinalIgnoreCase.Equals(backup.PackageId, package.PackageRegistration.Id)
                || !StringComparer.OrdinalIgnoreCase.Equals(backup.NormalizedVersion, package.NormalizedVersion))
            {
                return null;
            }

            if (package.PackageStatusKey == PackageStatus.Deleted && backup.Symbols.Any(saved => !package.SymbolPackages.Any(symbol =>
                symbol.Key == saved.SymbolPackageKey && symbol.StatusKey == PackageStatus.Deleted)))
            {
                return null;
            }

            return backup;
        }

        public async Task UndoAsync(Package package, int packageDeleteKey, string reason, string callerIdentity = null)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            if (packageDeleteKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packageDeleteKey));
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("An undo reason is required.", nameof(reason));
            }

            try
            {
                if (_configuration.ReadOnlyMode)
                {
                    throw new ReadOnlyModeException("Soft-delete undo is unavailable in read-only mode.");
                }

                PackageDeleteBackup backup;
                using (new SuspendDbExecutionStrategy())
                using (var transaction = _entitiesContext.GetDatabase().BeginTransaction())
                {
                    var currentStatus = _entitiesContext.Packages.Where(p => p.Key == package.Key)
                        .Select(p => (PackageStatus?)p.PackageStatusKey).SingleOrDefault();
                    if (currentStatus != package.PackageStatusKey)
                    {
                        throw new InvalidOperationException("The package state changed. Reload the package before retrying undo.");
                    }
                    backup = await ReadBackupAsync(package);
                    if (backup == null || backup.PackageDeleteKey != packageDeleteKey)
                    {
                        throw new InvalidOperationException("The package has no matching recovery data for this deletion.");
                    }
                    if (package.PackageStatusKey != PackageStatus.Deleted)
                    {
                        return;
                    }

                    await RestoreStateAsync(package, backup);
                    transaction.Commit();
                }

                await ReflowOrRevalidateAsync(package, backup, reason, callerIdentity);

                await _auditing.SaveAuditRecordAsync(new PackageUndoSoftDeleteAuditRecord(
                    package, packageDeleteKey, reason, success: true, callerIdentity: callerIdentity));
            }
            catch (Exception exception)
            {
                _diagnostics.ExceptionEvent(exception);
                try
                {
                    await _auditing.SaveAuditRecordAsync(new PackageUndoSoftDeleteAuditRecord(
                        package, packageDeleteKey, reason, success: false, callerIdentity: callerIdentity));
                }
                catch (Exception auditException)
                {
                    _diagnostics.ExceptionEvent(auditException);
                }
                throw;
            }
        }

        private async Task RestoreStateAsync(Package package, PackageDeleteBackup backup)
        {
            if (!_configuration.AsynchronousPackageValidationEnabled
                && (backup.PackageStatusKey == PackageStatus.Validating
                    || backup.Symbols.Any(s => s.PackageStatusKey == PackageStatus.Validating)))
            {
                throw new InvalidOperationException("Asynchronous validation must be enabled to restore a validating package.");
            }

            await RestoreFilesAsync(package, backup);
            package.Listed = backup.Listed;
            await _packageService.UpdatePackageStatusAsync(package, backup.PackageStatusKey, commitChanges: false);
            foreach (var saved in backup.Symbols)
            {
                var symbol = package.SymbolPackages.Single(s => s.Key == saved.SymbolPackageKey);
                var published = symbol.Published;
                await _symbolPackageService.UpdateStatusAsync(symbol, saved.PackageStatusKey, commitChanges: false);
                symbol.Published = published;
            }

            var changedPackages = RestoreDeprecations(package, backup);
            await _entitiesContext.SaveChangesAsync();
            if (changedPackages.Count > 0)
            {
                await _packageUpdates.UpdatePackagesAsync(changedPackages);
            }
        }

        private async Task ReflowOrRevalidateAsync(Package package, PackageDeleteBackup backup, string reason, string callerIdentity)
        {
            // Reflow owns its transaction, so run follow-up only after committing the restored state.
            if (backup.PackageStatusKey == PackageStatus.Available)
            {
                var restored = await _reflow.ReflowAsync(package.Id, package.NormalizedVersion, reason, callerIdentity);
                if (restored == null)
                {
                    throw new InvalidOperationException("The restored package could not be found for reflow.");
                }
            }
            else if (backup.PackageStatusKey == PackageStatus.Validating && package.PackageStatusKey == PackageStatus.Validating)
            {
                await _validation.RevalidateAsync(package);
            }

            foreach (var saved in backup.Symbols.Where(s => s.PackageStatusKey == PackageStatus.Validating))
            {
                var symbol = package.SymbolPackages.Single(s => s.Key == saved.SymbolPackageKey);
                if (symbol.StatusKey == PackageStatus.Validating)
                {
                    await _validation.RevalidateAsync(symbol);
                }
            }
        }

        private async Task RestoreFilesAsync(Package package, PackageDeleteBackup backup)
        {
            var validation = backup.PackageStatusKey != PackageStatus.Available;
            if (validation && await _packageFiles.DoesPackageFileExistAsync(package))
            {
                throw new InvalidOperationException("A private package cannot be restored over an existing public archive.");
            }
            await _packageFiles.RestorePackageFileFromBackupAsync(package,
                validation ? backup.ValidationBackupHash : backup.PublicBackupHash, validation);

            foreach (var saved in backup.Symbols)
            {
                var privateSymbols = saved.PackageStatusKey != PackageStatus.Available;
                await _symbolFiles.RestorePackageFileFromBackupAsync(package,
                    privateSymbols ? saved.ValidationBackupHash : saved.PublicBackupHash, privateSymbols);
            }

            if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent || package.HasEmbeddedReadme)
            {
                using (var stream = validation
                    ? await _packageFiles.DownloadValidationPackageFileAsync(package)
                    : await _packageFiles.DownloadPackageFileAsync(package))
                {
                    if (stream == null)
                    {
                        throw new FileNotFoundException("The restored package archive was not found for content extraction.");
                    }
                    if (package.EmbeddedLicenseType != EmbeddedLicenseFileType.Absent)
                    {
                        await _licenses.ExtractAndSaveLicenseFileAsync(package, stream);
                    }
                    if (package.HasEmbeddedReadme)
                    {
                        await _readmes.ExtractAndSaveReadmeFileAsync(package, stream);
                    }
                }
            }
            if (!package.HasEmbeddedReadme && !string.IsNullOrWhiteSpace(backup.ExternalReadmeMarkdown))
            {
                await _packageFiles.SaveReadMeMdFileAsync(package, backup.ExternalReadmeMarkdown);
            }
        }

        private List<Package> RestoreDeprecations(Package package, PackageDeleteBackup backup)
        {
            var changed = new List<Package>();
            var keys = backup.IncomingDeprecationKeys;
            var deprecations = _entitiesContext.Deprecations.Where(d => keys.Contains(d.Key)).ToList();
            foreach (var deprecation in deprecations)
            {
                // Keep newer replacement choices and edits, and never recreate removed deprecation rows.
                if (deprecation.AlternatePackageKey.HasValue
                    || deprecation.AlternatePackageRegistrationKey != package.PackageRegistration.Key
                    || deprecation.Package.LastEdited > backup.DeletedOnUtc)
                {
                    continue;
                }
                deprecation.AlternatePackage = package;
                deprecation.AlternatePackageKey = package.Key;
                changed.Add(deprecation.Package);
            }
            return changed.Distinct().ToList();
        }
    }
}