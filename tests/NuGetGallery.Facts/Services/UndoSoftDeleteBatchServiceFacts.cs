// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery
{
    public class UndoSoftDeleteBatchServiceFacts
    {
        public class ThePreviewAsyncMethod : FactsBase
        {
            [Fact]
            public async Task ReturnsOnlySelectedBackupsWithoutRestoring()
            {
                Requests[1].PackageDeleteKey = 0;

                var result = await Target.PreviewAsync(new[] { Requests[1] });

                Assert.Same(Backups[1], Assert.Single(result));
                Undo.Verify(x => x.GetBackupAsync(Packages[1]), Times.Once);
                Undo.VerifyNoOtherCalls();
                PackageService.Verify(x => x.FindPackageByIdAndVersionStrict(Requests[1].Id, Requests[1].Version), Times.Once);
                PackageService.VerifyNoOtherCalls();
            }

            [Theory]
            [InlineData("null")]
            [InlineData("empty")]
            [InlineData("null-item")]
            [InlineData("id")]
            [InlineData("missing")]
            [InlineData("duplicate")]
            public async Task RejectsInvalidSelectionsBeforeAnyRestore(string invalid)
            {
                IReadOnlyList<UndoPackageRequest> requests = Requests;
                switch (invalid)
                {
                    case "null":
                        requests = null;
                        break;
                    case "empty":
                        requests = Array.Empty<UndoPackageRequest>();
                        break;
                    case "null-item":
                        Requests[2] = null;
                        break;
                    case "id":
                        Requests[2].Id = " ";
                        break;
                    case "missing":
                        Requests[2].Version = "99.0.0";
                        break;
                    case "duplicate":
                        Requests[2] = new UndoPackageRequest { Id = "PACKAGE", Version = "1.0.0" };
                        break;
                }

                await Assert.ThrowsAsync<UserSafeException>(() => Target.PreviewAsync(requests));
                await Assert.ThrowsAsync<UserSafeException>(() => Target.UndoAsync(requests, Reason, Caller));

                Undo.Verify(x => x.UndoAsync(It.IsAny<Package>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task ReportsThePackageWithMissingRecoveryData()
            {
                Undo.Setup(x => x.GetBackupAsync(Packages[1])).ReturnsAsync((PackageDeleteBackup)null);

                var exception = await Assert.ThrowsAsync<UserSafeException>(() => Target.PreviewAsync(Requests));

                Assert.Equal("Package 2.0.0 has no matching recovery data for its latest committed deletion. Undo soft delete is unavailable.", exception.Message);
                Undo.Verify(x => x.UndoAsync(It.IsAny<Package>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task PropagatesRecoveryReadErrors()
            {
                var expected = new InvalidOperationException("Storage failed.");
                Undo.Setup(x => x.GetBackupAsync(Packages[1])).ThrowsAsync(expected);

                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Target.PreviewAsync(Requests));

                Assert.Same(expected, exception);
                Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => Target.UndoAsync(Requests, Reason, Caller)));
                Undo.Verify(x => x.UndoAsync(It.IsAny<Package>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
        }

        public class TheUndoAsyncMethod : FactsBase
        {
            [Fact]
            public async Task PreflightsEveryPackageBeforeRestoringInSelectionOrder()
            {
                var calls = new List<string>();
                for (var i = 0; i < Packages.Length; i++)
                {
                    var index = i;
                    PackageService.Setup(x => x.FindPackageByIdAndVersionStrict(Requests[index].Id, Requests[index].Version))
                        .Callback(() => calls.Add("find " + index))
                        .Returns(Packages[index]);
                    Undo.Setup(x => x.GetBackupAsync(Packages[index]))
                        .Callback(() => calls.Add("preview " + index))
                        .ReturnsAsync(Backups[index]);
                    Undo.Setup(x => x.UndoAsync(Packages[index], Backups[index].PackageDeleteKey, Reason, Caller))
                        .Callback(() => calls.Add("undo " + index))
                        .Returns(Task.CompletedTask);
                }

                var result = await Target.UndoAsync(Requests, Reason, Caller);

                Assert.True(result.Success);
                Assert.Null(result.FailedPackage);
                Assert.Null(result.Exception);
                Assert.Equal(new[] { "find 0", "preview 0", "find 1", "preview 1", "find 2", "preview 2", "undo 0", "undo 1", "undo 2" }, calls);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(0)]
            [InlineData(99)]
            public async Task RejectsStaleOrMissingRecoveryBeforeAnyRestore(int? packageDeleteKey)
            {
                if (packageDeleteKey.HasValue)
                {
                    Requests[2].PackageDeleteKey = packageDeleteKey.Value;
                }
                else
                {
                    Undo.Setup(x => x.GetBackupAsync(Packages[2])).ReturnsAsync((PackageDeleteBackup)null);
                }

                await Assert.ThrowsAsync<UserSafeException>(() => Target.UndoAsync(Requests, Reason, Caller));

                Undo.Verify(x => x.UndoAsync(It.IsAny<Package>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }

            [Fact]
            public async Task StopsAtTheFirstFailureWithoutReusingTheFailedContext()
            {
                const int failedIndex = 1;
                var expected = new InvalidOperationException("Follow-up failed.");
                Undo.Setup(x => x.UndoAsync(Packages[failedIndex], Backups[failedIndex].PackageDeleteKey, Reason, Caller))
                    .ThrowsAsync(expected);

                var result = await Target.UndoAsync(Requests, Reason, Caller);

                Assert.False(result.Success);
                Assert.Same(Requests[failedIndex], result.FailedPackage);
                Assert.Same(expected, result.Exception);
                for (var i = 0; i < Packages.Length; i++)
                {
                    Undo.Verify(x => x.UndoAsync(Packages[i], Backups[i].PackageDeleteKey, Reason, Caller),
                        i <= failedIndex ? Times.Once() : Times.Never());
                }
            }
        }

        public abstract class FactsBase
        {
            protected const string Reason = "Restore accidentally deleted packages";
            protected const string Caller = "authenticated-admin";
            protected readonly Package[] Packages;
            protected readonly UndoPackageRequest[] Requests;
            protected readonly PackageDeleteBackup[] Backups;
            protected readonly Mock<IPackageService> PackageService = new Mock<IPackageService>(MockBehavior.Strict);
            protected readonly Mock<IUndoSoftDeleteService> Undo = new Mock<IUndoSoftDeleteService>(MockBehavior.Strict);
            protected readonly UndoSoftDeleteBatchService Target;

            protected FactsBase()
            {
                Packages = Enumerable.Range(1, 3).Select(i => new Package
                {
                    Key = i,
                    PackageRegistration = new PackageRegistration { Id = "Package" },
                    NormalizedVersion = i + ".0.0",
                    PackageStatusKey = PackageStatus.Deleted
                }).ToArray();
                Requests = Packages.Select(p => new UndoPackageRequest
                {
                    Id = p.PackageRegistration.Id,
                    Version = p.NormalizedVersion,
                    PackageDeleteKey = p.Key + 10
                }).ToArray();
                Backups = Packages.Select(p => new PackageDeleteBackup
                {
                    PackageId = p.PackageRegistration.Id,
                    NormalizedVersion = p.NormalizedVersion,
                    PackageKey = p.Key,
                    PackageDeleteKey = p.Key + 10
                }).ToArray();
                PackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((string id, string version) => Packages.SingleOrDefault(p =>
                        StringComparer.OrdinalIgnoreCase.Equals(p.PackageRegistration.Id, id) && p.NormalizedVersion == version));
                for (var i = 0; i < Packages.Length; i++)
                {
                    Undo.Setup(x => x.GetBackupAsync(Packages[i])).ReturnsAsync(Backups[i]);
                    Undo.Setup(x => x.UndoAsync(Packages[i], Backups[i].PackageDeleteKey, Reason, Caller))
                        .Returns(Task.CompletedTask);
                }
                Target = new UndoSoftDeleteBatchService(PackageService.Object, Undo.Object);
            }
        }
    }
}