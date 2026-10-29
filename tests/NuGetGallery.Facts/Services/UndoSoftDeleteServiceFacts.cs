// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class UndoSoftDeleteServiceFacts
    {
        [Fact]
        public async Task GetBackupRejectsNullPackage()
        {
            var fixture = new Fixture();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.GetBackupAsync(null));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public async Task UndoRejectsNullPackage()
        {
            var fixture = new Fixture();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Service.UndoAsync(null, 10, "Restore"));

            Assert.Equal("package", exception.ParamName);
        }

        [Fact]
        public async Task ReturnsBackupForMatchingCommittedDeletionAndRetainedSymbols()
        {
            var fixture = new Fixture();
            fixture.Package.SymbolPackages.Add(new SymbolPackage { Key = 30, StatusKey = PackageStatus.Deleted });
            fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup { SymbolPackageKey = 30 });

            var backup = await fixture.Service.GetBackupAsync(fixture.Package);

            Assert.Same(fixture.Backup, backup);
            fixture.Backups.Verify(x => x.GetAsync(10, 20), Times.Once);
            fixture.Backups.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task NonDeletedPackageDoesNotReadRecoveryJson()
        {
            var fixture = new Fixture();
            fixture.Package.PackageStatusKey = PackageStatus.Available;

            Assert.Null(await fixture.Service.GetBackupAsync(fixture.Package));

            fixture.Backups.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task FailedAttemptJsonWithoutCommittedAssociationDoesNotEnableUndo()
        {
            var fixture = new Fixture();
            fixture.Deletion.Packages.Clear();

            Assert.Null(await fixture.Service.GetBackupAsync(fixture.Package));

            fixture.Backups.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task LatestDeletionWithoutJsonDoesNotFallBackToOlderBackup()
        {
            var fixture = new Fixture();
            var newer = new PackageDelete { Key = 11, DeletedOn = fixture.Deletion.DeletedOn.AddMinutes(1) };
            newer.Packages.Add(fixture.Package);
            fixture.Deletions.Setup(x => x.GetAll()).Returns(new[] { fixture.Deletion, newer }.AsQueryable());

            Assert.Null(await fixture.Service.GetBackupAsync(fixture.Package));

            fixture.Backups.Verify(x => x.GetAsync(11, 20), Times.Once);
            fixture.Backups.Verify(x => x.GetAsync(10, 20), Times.Never);
        }

        [Theory]
        [InlineData("id")]
        [InlineData("version")]
        [InlineData("timestamp")]
        [InlineData("symbol")]
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
                    break;
            }

            Assert.Null(await fixture.Service.GetBackupAsync(fixture.Package));
        }

        [Theory]
        [InlineData(PackageStatus.Available, true)]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.Validating, true)]
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
            var symbol = new SymbolPackage { Key = 30, StatusKey = PackageStatus.Deleted, Published = published };
            var olderSymbol = new SymbolPackage { Key = 29, StatusKey = PackageStatus.Deleted };
            fixture.Package.SymbolPackages.Add(symbol);
            fixture.Package.SymbolPackages.Add(olderSymbol);

            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore package", "admin");
            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore package", "admin");

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
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore package", "admin"),
                status == PackageStatus.Available ? Times.Once() : Times.Never());
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(fixture.Package),
                status == PackageStatus.Validating ? Times.Once() : Times.Never());
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(symbol),
                status == PackageStatus.Validating ? Times.Once() : Times.Never());
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success)), Times.Once);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AuditsAuthenticatedCallerAndReason(bool failReflow)
        {
            var fixture = new Fixture();
            if (failReflow)
            {
                fixture.Dependency<IReflowPackageService>().Setup(x => x.ReflowAsync(
                    fixture.Package.Id, "1.0.0", "Restore", "admin"))
                    .ThrowsAsync(new IOException("Reflow failed"));
            }

            var operation = fixture.Service.UndoAsync(fixture.Package, 10, "Restore", "admin");
            if (failReflow)
            {
                await Assert.ThrowsAsync<IOException>(() => operation);
            }
            else
            {
                await operation;
            }

            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(record => record.Success == !failReflow
                    && record.Reason == "Restore"
                    && record.CallerIdentity == "admin")), Times.Once);
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore", "admin"), Times.Once);
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

            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore content");

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
            var rows = Enumerable.Range(40, 4).Select(key => new PackageDeprecation
            {
                Key = key,
                AlternatePackageRegistrationKey = fixture.Package.PackageRegistration.Key,
                Package = new Package { Key = key + 100, LastEdited = fixture.Deletion.DeletedOn.AddDays(-1) }
            }).ToArray();
            rows[1].AlternatePackageKey = 999;
            rows[2].AlternatePackageRegistrationKey = 999;
            rows[3].Package.LastEdited = fixture.Deletion.DeletedOn.AddDays(1);
            fixture.Context.Deprecations.AddRange(rows);
            fixture.Backup.IncomingDeprecationKeys.AddRange(rows.Select(d => d.Key));
            fixture.Backup.IncomingDeprecationKeys.Add(404);

            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore recommendation");

            Assert.Equal(fixture.Package.Key, rows[0].AlternatePackageKey);
            Assert.Equal(999, rows[1].AlternatePackageKey);
            Assert.Null(rows[2].AlternatePackageKey);
            Assert.Null(rows[3].AlternatePackageKey);
            Assert.Equal(4, fixture.Context.Deprecations.Count());
            fixture.Dependency<IPackageUpdateService>().Verify(x => x.UpdatePackagesAsync(
                It.Is<IReadOnlyList<Package>>(p => p.Count == 1 && p[0] == rows[0].Package), true), Times.Once);
        }

        [Fact]
        public async Task FailedReflowIsReportedWithoutRepeatingFollowUpOnAnotherUndo()
        {
            var fixture = new Fixture();
            fixture.Dependency<IReflowPackageService>().Setup(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore", null))
                .ThrowsAsync(new IOException("Reflow failed"));

            await Assert.ThrowsAsync<IOException>(() => fixture.Service.UndoAsync(fixture.Package, 10, "Restore"));
            Assert.Equal(PackageStatus.Available, fixture.Package.PackageStatusKey);
            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore");

            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, "public-package", false), Times.Once);
            fixture.Dependency<IReflowPackageService>().Verify(x => x.ReflowAsync(
                fixture.Package.Id, "1.0.0", "Restore", null), Times.Once);
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success)), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success)), Times.Never);
        }

        [Theory]
        [InlineData("read-only")]
        [InlineData("stale-deletion")]
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
                case "stale-deletion":
                    fixture.Backup.PackageDeleteKey = 9;
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

            await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.UndoAsync(fixture.Package, 10, "Restore"));

            fixture.Dependency<IPackageFileService>().VerifyNoOtherCalls();
            fixture.Transaction.Verify(x => x.Commit(), Times.Never);
        }

        [Fact]
        public async Task FailedValidationRequestIsReportedWithoutRepeatingFollowUpOnAnotherUndo()
        {
            var fixture = new Fixture();
            fixture.Backup.PackageStatusKey = PackageStatus.Validating;
            fixture.Backup.Listed = false;
            fixture.Dependency<IValidationService>().Setup(x => x.RevalidateAsync(fixture.Package))
                .ThrowsAsync(new IOException("Queue unavailable"));

            await Assert.ThrowsAsync<IOException>(() => fixture.Service.UndoAsync(fixture.Package, 10, "Restore"));
            Assert.Equal(PackageStatus.Validating, fixture.Package.PackageStatusKey);
            await fixture.Service.UndoAsync(fixture.Package, 10, "Restore");

            Assert.False(fixture.Package.Listed);
            fixture.Dependency<IValidationService>().Verify(x => x.RevalidateAsync(fixture.Package), Times.Once);
            fixture.Dependency<IPackageFileService>().Verify(x => x.RestorePackageFileFromBackupAsync(
                fixture.Package, "private-package", true), Times.Once);
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Transaction.Verify(x => x.Commit(), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success)), Times.Once);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => r.Success)), Times.Never);
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

            var actual = await Assert.ThrowsAsync<IOException>(() => fixture.Service.UndoAsync(fixture.Package, 10, "Restore"));

            Assert.Same(expected, actual);
            fixture.Dependency<IValidationService>().VerifyNoOtherCalls();
            fixture.Dependency<IReflowPackageService>().VerifyNoOtherCalls();
            fixture.Backups.Verify(x => x.SaveAsync(It.IsAny<PackageDeleteBackup>()), Times.Never);
            fixture.Dependency<IAuditingService>().Verify(x => x.SaveAuditRecordAsync(
                It.Is<PackageUndoSoftDeleteAuditRecord>(r => !r.Success)), Times.Once);
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
                Backups.Setup(x => x.GetAsync(10, 20)).ReturnsAsync(Backup);
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
                Service = Get<UndoSoftDeleteService>();
            }

            public Mock<T> Dependency<T>() where T : class => GetMock<T>();
        }
    }
}