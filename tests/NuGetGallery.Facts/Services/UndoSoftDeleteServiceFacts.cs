// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class UndoSoftDeleteServiceFacts
    {
        [Fact]
        public async Task RecoveryWithoutCommittedDeletionCannotBeRestored()
        {
            var fixture = new Fixture();
            fixture.Deletion.Packages.Clear();

            var exception = await Assert.ThrowsAsync<UserSafeException>(() => fixture.Service.UndoAsync(
                fixture.Package, "Restore"));

            Assert.Contains("latest committed deletion", exception.Message);
            fixture.Deletions.Verify(x => x.GetAll(), Times.Once);
            fixture.Backups.VerifyNoOtherCalls();
            fixture.Dependency<IPackageFileService>().VerifyNoOtherCalls();
            fixture.Dependency<IAuditingService>().VerifyNoOtherCalls();
            fixture.Transaction.Verify(x => x.Commit(), Times.Never);
        }

        [Fact]
        public async Task RestoresLatestCommittedDeletionWithoutReadingOlderBackup()
        {
            var fixture = new Fixture();
            var newer = new PackageDelete { Key = 11, DeletedOn = fixture.Deletion.DeletedOn.AddMinutes(1) };
            newer.Packages.Add(fixture.Package);
            fixture.Deletions.Setup(x => x.GetAll()).Returns(new[] { fixture.Deletion, newer }.AsQueryable());
            var newerBackup = new PackageDeleteBackup
            {
                PackageDeleteKey = 11,
                PackageKey = fixture.Package.Key,
                PackageId = fixture.Package.Id,
                NormalizedVersion = fixture.Package.NormalizedVersion,
                DeletedOnUtc = newer.DeletedOn,
                PackageStatusKey = PackageStatus.Available,
                PublicBackupHash = "newer-public-package",
                Listed = true
            };
            fixture.Backups.Setup(x => x.GetAsync("Example.Package", "1.0.0", 11, 20)).ReturnsAsync(newerBackup);

            await fixture.Service.UndoAsync(fixture.Package, "Restore", "admin");

            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 10, 20), Times.Never);
            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 11, 20), Times.Once);
            fixture.Deletions.Verify(x => x.GetAll(), Times.Once);
            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, "newer-public-package", false), Times.Once);
            Assert.Equal(PackageStatus.Available, fixture.Package.PackageStatusKey);
            Assert.True(fixture.Package.Listed);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success && r.PackageDeleteKey == 11
                    && r.Reason == "Restore" && r.CallerIdentity == "admin")), Times.Once);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("malformed")]
        [InlineData("unsupported")]
        [InlineData("invalid-hash")]
        public async Task InvalidLatestRecoveryIsAuditedBeforeAnyFileChanges(string failure)
        {
            var fixture = new Fixture();
            var newer = new PackageDelete { Key = 11, DeletedOn = fixture.Deletion.DeletedOn.AddMinutes(1) };
            newer.Packages.Add(fixture.Package);
            fixture.Deletions.Setup(x => x.GetAll()).Returns(new[] { fixture.Deletion, newer }.AsQueryable());
            var read = fixture.Backups.Setup(x => x.GetAsync("Example.Package", "1.0.0", 11, 20));
            Exception expected = null;
            switch (failure)
            {
                case "missing":
                    read.ReturnsAsync((PackageDeleteBackup)null);
                    break;
                case "malformed":
                    expected = new JsonSerializationException("Invalid recovery JSON");
                    break;
                case "unsupported":
                    expected = new InvalidDataException("Unsupported deletion backup schema.");
                    break;
                case "invalid-hash":
                    expected = new FormatException("Invalid backup hash");
                    break;
            }
            if (expected != null)
            {
                read.ThrowsAsync(expected);
            }

            var exception = await Assert.ThrowsAsync<UserSafeException>(() => fixture.Service.UndoAsync(
                fixture.Package, "Restore", "admin"));

            Assert.Contains("latest committed deletion", exception.Message);
            Assert.Same(expected, exception.InnerException);
            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 11, 20), Times.Once);
            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 10, 20), Times.Never);
            fixture.Deletions.Verify(x => x.GetAll(), Times.Once);
            fixture.Dependency<IPackageFileService>().VerifyNoOtherCalls();
            fixture.Dependency<ISymbolPackageFileService>().VerifyNoOtherCalls();
            fixture.Dependency<ICoreLicenseFileService>().VerifyNoOtherCalls();
            fixture.Dependency<ICoreReadmeFileService>().VerifyNoOtherCalls();
            fixture.Dependency<IReflowPackageService>().VerifyNoOtherCalls();
            fixture.Dependency<IValidationService>().VerifyNoOtherCalls();
            Assert.Equal(PackageStatus.Deleted, fixture.Package.PackageStatusKey);
            fixture.Transaction.Verify(x => x.Commit(), Times.Never);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success && r.PackageDeleteKey == 11
                    && r.Reason == "Restore" && r.CallerIdentity == "admin")), Times.Once);
        }

        [Theory]
        [InlineData("id")]
        [InlineData("version")]
        [InlineData("timestamp")]
        [InlineData("symbol")]
        [InlineData("symbol-package")]
        [InlineData("deletion-key")]
        [InlineData("package-key")]
        public async Task BackupConflictsDoNotEnableUndo(string conflict)
        {
            var fixture = new Fixture();
            switch (conflict)
            {
                case "id":
                    fixture.Backup.PackageId = "Other.Package";
                    break;
                case "version":
                    fixture.Backup.NormalizedVersion = "2.0.0";
                    break;
                case "timestamp":
                    fixture.Backup.DeletedOnUtc = fixture.Backup.DeletedOnUtc.AddSeconds(1);
                    break;
                case "symbol":
                    fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup { SymbolPackageKey = 999 });
                    fixture.Context.SymbolPackages.Add(new SymbolPackage { Key = 999, PackageKey = fixture.Package.Key, StatusKey = PackageStatus.Available });
                    break;
                case "symbol-package":
                    fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup { SymbolPackageKey = 999 });
                    fixture.Context.SymbolPackages.Add(new SymbolPackage { Key = 999, PackageKey = 999, StatusKey = PackageStatus.Deleted });
                    break;
                case "deletion-key":
                    fixture.Backup.PackageDeleteKey = 9;
                    break;
                case "package-key":
                    fixture.Backup.PackageKey = 19;
                    break;
            }

            await Assert.ThrowsAsync<UserSafeException>(() => fixture.Service.UndoAsync(fixture.Package, "Restore"));

            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 10, 20), Times.Once);
            fixture.Dependency<IPackageFileService>().VerifyNoOtherCalls();
            fixture.Dependency<ISymbolPackageFileService>().VerifyNoOtherCalls();
            fixture.Transaction.Verify(x => x.Commit(), Times.Never);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success && r.PackageDeleteKey == 10)), Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.Validating, false)]
        [InlineData(PackageStatus.FailedValidation, true)]
        public async Task RestoresPackageWithoutSymbolsRemovedByCleanup(PackageStatus symbolStatus, bool staleNavigation)
        {
            var fixture = new Fixture();
            var saved = new SymbolPackageDeleteBackup
            {
                SymbolPackageKey = 30,
                SymbolPackageStatusKey = symbolStatus,
                PublicBackupHash = "public-symbols",
                ValidationBackupHash = "private-symbols"
            };
            fixture.Backup.Symbols.Add(saved);
            fixture.Dependency<IAppConfiguration>().SetupGet(x => x.AsynchronousPackageValidationEnabled).Returns(false);
            if (staleNavigation)
            {
                fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 30, PackageKey = fixture.Package.Key, StatusKey = PackageStatus.Deleted });
            }

            await fixture.Service.UndoAsync(fixture.Package, "Restore", "admin");

            Assert.Equal(PackageStatus.Available, fixture.Package.PackageStatusKey);
            Assert.Empty(fixture.Backup.Symbols);
            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, "public-package", false), Times.Once);
            fixture.Dependency<ISymbolPackageFileService>().VerifyNoOtherCalls();
            fixture.Dependency<ISymbolPackageService>().VerifyNoOtherCalls();
            fixture.Dependency<IValidationService>().VerifyNoOtherCalls();
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore", "admin"), Times.Once);
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(record => record.Success)), Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available, true)]
        [InlineData(PackageStatus.Validating, false)]
        [InlineData(PackageStatus.FailedValidation, false)]
        public async Task RestoresSavedStateAndListingWithoutChangingPublicationHistory(PackageStatus status, bool listed)
        {
            var fixture = new Fixture();
            fixture.Backup.PackageStatusKey = status;
            fixture.Backup.Listed = listed;
            fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup
            {
                SymbolPackageKey = 30,
                SymbolPackageStatusKey = status,
                PublicBackupHash = "public-symbols",
                ValidationBackupHash = "private-symbols"
            });
            var published = fixture.Deletion.DeletedOn.AddDays(-1);
            fixture.Package.Published = published;
            fixture.Package.DownloadCount = 123;
            var symbol = new SymbolPackage { Key = 30, PackageKey = fixture.Package.Key, StatusKey = PackageStatus.Deleted, Published = published };
            var olderSymbol = new SymbolPackage { Key = 29, StatusKey = PackageStatus.Deleted };
            fixture.Package.SymbolPackages.Add(symbol);
            fixture.Package.SymbolPackages.Add(olderSymbol);
            fixture.Context.SymbolPackages.Add(symbol);
            fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup
            {
                SymbolPackageKey = 31,
                SymbolPackageStatusKey = status,
                PublicBackupHash = "missing-public-symbols",
                ValidationBackupHash = "missing-private-symbols"
            });

            await fixture.Service.UndoAsync(fixture.Package, "Restore package", "admin");
            await fixture.Service.UndoAsync(fixture.Package, "Restore package", "admin");

            Assert.Equal(status, fixture.Package.PackageStatusKey);
            Assert.Equal(listed, fixture.Package.Listed);
            Assert.Equal(published, fixture.Package.Published);
            Assert.Equal(123, fixture.Package.DownloadCount);
            Assert.Equal(status, symbol.StatusKey);
            Assert.Equal(published, symbol.Published);
            Assert.Equal(PackageStatus.Deleted, olderSymbol.StatusKey);
            var validation = status != PackageStatus.Available;
            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, validation ? "private-package" : "public-package", validation), Times.Once);
            fixture.Dependency<ISymbolPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, validation ? "private-symbols" : "public-symbols", validation), Times.Once);
            fixture.Dependency<ISymbolPackageFileService>().VerifyNoOtherCalls();
            Assert.Equal(30, Assert.Single(fixture.Backup.Symbols).SymbolPackageKey);
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore package", "admin"),
                status == PackageStatus.Available ? Times.Once() : Times.Never());
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(fixture.Package),
                status == PackageStatus.Validating ? Times.Once() : Times.Never());
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(symbol),
                status == PackageStatus.Validating ? Times.Once() : Times.Never());
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 10, 20), Times.Once);
            fixture.Deletions.Verify(x => x.GetAll(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success && r.PackageDeleteKey == 10)), Times.Once);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RestoresExternalOrEmbeddedContent(bool embedded)
        {
            var fixture = new Fixture();
            fixture.Backup.ExternalReadmeMarkdown = "# Restored README";
            if (embedded)
            {
                fixture.Package.HasReadMe = true;
                fixture.Package.EmbeddedReadmeType = EmbeddedReadmeFileType.Markdown;
                fixture.Package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                fixture.Dependency<IPackageFileService>().Setup(x => x.DownloadPackageFileAsync(fixture.Package))
                    .ReturnsAsync(new MemoryStream());
            }

            await fixture.Service.UndoAsync(fixture.Package, "Restore content");

            fixture.Dependency<IPackageFileService>().Verify(x => x.SaveReadMeMdFileAsync(
                fixture.Package, "# Restored README"), embedded ? Times.Never() : Times.Once());
            fixture.Dependency<ICoreReadmeFileService>().Verify(x => x.ExtractAndSaveReadmeFileAsync(
                fixture.Package, It.IsAny<Stream>()), embedded ? Times.Once() : Times.Never());
            fixture.Dependency<ICoreLicenseFileService>().Verify(x => x.ExtractAndSaveLicenseFileAsync(
                fixture.Package, It.IsAny<Stream>()), embedded ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task RestoresRetainedDeprecationsWithoutOverwritingNewerChoices()
        {
            var fixture = new Fixture();
            var rows = Enumerable.Range(40, 5).Select(key => new PackageDeprecation
            {
                Key = key,
                Package = new Package { Key = key + 100, LastEdited = fixture.Deletion.DeletedOn.AddDays(-1) }
            }).ToArray();
            rows[1].AlternatePackageKey = 999;
            rows[2].AlternatePackageRegistrationKey = 999;
            rows[3].Package.LastEdited = fixture.Deletion.DeletedOn.AddDays(1);
            rows[4].AlternatePackageRegistrationKey = fixture.Package.PackageRegistration.Key;
            fixture.Context.Deprecations.AddRange(rows);
            fixture.Backup.IncomingDeprecationKeys.AddRange(rows.Select(d => d.Key));
            fixture.Backup.IncomingDeprecationKeys.Add(404);

            await fixture.Service.UndoAsync(fixture.Package, "Restore recommendation");

            Assert.Equal(fixture.Package.Key, rows[0].AlternatePackageKey);
            Assert.Equal(999, rows[1].AlternatePackageKey);
            Assert.Null(rows[2].AlternatePackageKey);
            Assert.Equal(fixture.Package.Key, rows[3].AlternatePackageKey);
            Assert.Null(rows[4].AlternatePackageKey);
            Assert.Equal(fixture.Package.PackageRegistration.Key, rows[4].AlternatePackageRegistrationKey);
            Assert.Equal(5, fixture.Context.Deprecations.Count());
            fixture.Dependency<IPackageUpdateService>().Verify(x => x.UpdatePackagesAsync(
                It.Is<IReadOnlyList<Package>>(p => p.Count == 2
                    && p[0] == rows[0].Package
                    && p[1] == rows[3].Package), true), Times.Once);
        }

        [Theory]
        [InlineData(PackageStatus.Available)]
        [InlineData(PackageStatus.Validating)]
        public async Task FailedFollowUpIsAuditedWithoutRepeatingItOnAnotherUndo(PackageStatus status)
        {
            var fixture = new Fixture();
            fixture.Backup.PackageStatusKey = status;
            fixture.Backup.Listed = false;
            var expected = new IOException("Follow-up failed");
            if (status == PackageStatus.Available)
            {
                fixture.Dependency<IReflowPackageService>().Setup(x => x.ReflowAsync(
                    fixture.Package.Id, "1.0.0", "Restore", "admin")).ThrowsAsync(expected);
            }
            else
            {
                fixture.Dependency<IValidationService>().Setup(x => x.RevalidateAsync(fixture.Package))
                    .ThrowsAsync(expected);
            }

            var actual = await Assert.ThrowsAsync<IOException>(() => fixture.Service.UndoAsync(fixture.Package, "Restore", "admin"));
            Assert.Same(expected, actual);
            Assert.Equal(status, fixture.Package.PackageStatusKey);
            await fixture.Service.UndoAsync(fixture.Package, "Restore", "admin");

            Assert.False(fixture.Package.Listed);
            var validation = status == PackageStatus.Validating;
            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, validation ? "private-package" : "public-package", validation), Times.Once);
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore", "admin"), validation ? Times.Never() : Times.Once());
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(fixture.Package),
                validation ? Times.Once() : Times.Never());
            fixture.Backups.Verify(x => x.GetAsync("Example.Package", "1.0.0", 10, 20), Times.Once);
            fixture.Deletions.Verify(x => x.GetAll(), Times.Once);
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success && r.PackageDeleteKey == 10
                    && r.Reason == "Restore" && r.CallerIdentity == "admin")), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success)), Times.Never);
        }

        [Theory]
        [InlineData("read-only")]
        [InlineData("changed-state")]
        [InlineData("validation-disabled")]
        public async Task RejectsUnsafeRequestsBeforeRestoringFiles(string conflict)
        {
            var fixture = new Fixture();
            switch (conflict)
            {
                case "read-only":
                    fixture.Dependency<IAppConfiguration>().SetupGet(x => x.ReadOnlyMode).Returns(true);
                    break;
                case "changed-state":
                    fixture.Context.Packages.Remove(fixture.Package);
                    fixture.Context.Packages.Add(new Package { Key = 20, PackageStatusKey = PackageStatus.Available });
                    break;
                case "validation-disabled":
                    fixture.Backup.PackageStatusKey = PackageStatus.Validating;
                    fixture.Dependency<IAppConfiguration>().SetupGet(x => x.AsynchronousPackageValidationEnabled).Returns(false);
                    break;
            }

            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.UndoAsync(fixture.Package, "Restore"));

            fixture.Dependency<IPackageFileService>().VerifyNoOtherCalls();
            fixture.Transaction.Verify(x => x.Commit(), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task StorageOrCommitFailureIsAuditedAndDoesNotStartFollowUp(bool failCommit)
        {
            var fixture = new Fixture();
            var expected = new IOException("Restore failed");
            if (failCommit)
            {
                fixture.Transaction.Setup(x => x.Commit()).Throws(expected);
            }
            else
            {
                fixture.Dependency<IPackageFileService>().Setup(x => x.RestorePackageFileFromBackupAsync(
                    fixture.Package, "public-package", false)).ThrowsAsync(expected);
            }

            var actual = await Assert.ThrowsAsync<IOException>(() => fixture.Service.UndoAsync(fixture.Package, "Restore"));

            Assert.Same(expected, actual);
            fixture.Dependency<IValidationService>().VerifyNoOtherCalls();
            fixture.Dependency<IReflowPackageService>().VerifyNoOtherCalls();
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success)), Times.Once);
            fixture.Diagnostics.Verify(x => x.TraceEvent(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<string>(message => message.Contains("Soft-delete undo failed for Example.Package 1.0.0")
                    && message.Contains("package key 20, deletion key 10")),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
            fixture.Diagnostics.Verify(x => x.ExceptionEvent(expected), Times.Once);
        }

        private class Fixture : TestContainer
        {
            public readonly Package Package = new Package
            {
                Key = 20,
                PackageRegistration = new PackageRegistration { Key = 1, Id = "Example.Package" },
                Version = "1.0.0",
                NormalizedVersion = "1.0.0",
                PackageStatusKey = PackageStatus.Deleted
            };
            public readonly PackageDelete Deletion = new PackageDelete
            {
                Key = 10,
                DeletedOn = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc)
            };
            public readonly Mock<IEntityRepository<PackageDelete>> Deletions;
            public readonly Mock<IPackageDeleteBackupService> Backups;
            public readonly Mock<IDbContextTransaction> Transaction = new Mock<IDbContextTransaction>();
            public readonly Mock<IDiagnosticsSource> Diagnostics = new Mock<IDiagnosticsSource>();
            public readonly FakeEntitiesContext Context;
            public readonly PackageDeleteBackup Backup;
            public readonly UndoSoftDeleteService Service;

            public Fixture()
            {
                Context = GetFakeContext();
                Context.Packages.Add(Package);
                var database = new Mock<IDatabase>();
                database.Setup(x => x.BeginTransaction()).Returns(Transaction.Object);
                Context.SetupDatabase(database.Object);
                Deletions = GetMock<IEntityRepository<PackageDelete>>();
                Backups = GetMock<IPackageDeleteBackupService>();
                Deletion.Packages.Add(Package);
                Deletions.Setup(x => x.GetAll()).Returns(new[] { Deletion }.AsQueryable());
                Backup = new PackageDeleteBackup
                {
                    PackageDeleteKey = Deletion.Key,
                    PackageKey = Package.Key,
                    PackageId = Package.PackageRegistration.Id,
                    NormalizedVersion = Package.NormalizedVersion,
                    DeletedOnUtc = Deletion.DeletedOn,
                    PackageStatusKey = PackageStatus.Available,
                    PublicBackupHash = "public-package",
                    ValidationBackupHash = "private-package"
                };
                Backups.Setup(x => x.GetAsync("Example.Package", "1.0.0", 10, 20)).ReturnsAsync(Backup);
                GetMock<IAppConfiguration>().SetupGet(x => x.AsynchronousPackageValidationEnabled).Returns(true);
                GetMock<IPackageService>().Setup(x => x.UpdatePackageStatusAsync(Package, It.IsAny<PackageStatus>(), false))
                    .Callback<Package, PackageStatus, bool>((package, status, _) => package.PackageStatusKey = status)
                    .Returns(Task.CompletedTask);
                GetMock<ISymbolPackageService>().Setup(x => x.UpdateStatusAsync(It.IsAny<SymbolPackage>(), It.IsAny<PackageStatus>(), false))
                    .Callback<SymbolPackage, PackageStatus, bool>((symbol, status, _) =>
                    {
                        symbol.StatusKey = status;
                        symbol.Published = DateTime.UtcNow;
                    })
                    .Returns(Task.CompletedTask);
                GetMock<IReflowPackageService>().Setup(x => x.ReflowAsync(Package.Id, "1.0.0", It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(Package);
                GetMock<IAuditingService>();
                GetMock<IDiagnosticsService>().Setup(x => x.GetSource(nameof(UndoSoftDeleteService))).Returns(Diagnostics.Object);
                Service = Get<UndoSoftDeleteService>();
            }

            public Mock<T> Dependency<T>() where T : class => GetMock<T>();
        }
    }
}