// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class UndoSoftDeleteServiceFacts
    {
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
        [InlineData("package-key")]
        [InlineData("deletion-key")]
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
                case "package-key":
                    fixture.Backup.PackageKey++;
                    break;
                case "deletion-key":
                    fixture.Backup.PackageDeleteKey++;
                    break;
                case "symbol":
                    fixture.Backup.Symbols.Add(new SymbolPackageDeleteBackup { SymbolPackageKey = 999 });
                    break;
            }

            Assert.Null(await fixture.Service.GetBackupAsync(fixture.Package));
        }

        private class Fixture
        {
            public readonly Package Package = new Package
            {
                Key = 20,
                PackageRegistration = new PackageRegistration { Id = "Example.Package" },
                NormalizedVersion = "1.0.0",
                PackageStatusKey = PackageStatus.Deleted
            };
            public readonly PackageDelete Deletion = new PackageDelete
            {
                Key = 10,
                DeletedOn = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc)
            };
            public readonly Mock<IEntityRepository<PackageDelete>> Deletions = new Mock<IEntityRepository<PackageDelete>>();
            public readonly Mock<IPackageDeleteBackupService> Backups = new Mock<IPackageDeleteBackupService>();
            public readonly PackageDeleteBackup Backup;
            public readonly UndoSoftDeleteService Service;

            public Fixture()
            {
                Deletion.Packages.Add(Package);
                Deletions.Setup(x => x.GetAll()).Returns(new[] { Deletion }.AsQueryable());
                Backup = new PackageDeleteBackup
                {
                    PackageDeleteKey = Deletion.Key,
                    PackageKey = Package.Key,
                    PackageId = Package.PackageRegistration.Id,
                    NormalizedVersion = Package.NormalizedVersion,
                    DeletedOnUtc = Deletion.DeletedOn
                };
                Backups.Setup(x => x.GetAsync(10, 20)).ReturnsAsync(Backup);
                Service = new UndoSoftDeleteService(Deletions.Object, Backups.Object);
            }
        }
    }
}