// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
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
            public async Task ReturnsCanonicalIdentitiesWithoutReadingRecoveryData()
            {
                Entries[0].Package.PackageRegistration.Id = "Canonical.Package";
                Requests[0].Id = " Package ";
                Requests[0].Version = " 1.0 ";

                IReadOnlyList<UndoPackageRequest> result = await Target.PreviewAsync(Requests);

                Assert.Equal(new[] { "preview find 0", "preview find 1", "preview find 2" }, Calls);
                Assert.Equal(3, result.Count);
                for (var i = 0; i < result.Count; i++)
                {
                    Assert.IsType<UndoPackageRequest>(result[i]);
                    Assert.NotSame(Requests[i], result[i]);
                    Assert.Equal(Entries[i].Package.PackageRegistration.Id, result[i].Id);
                    Assert.Equal(Entries[i].Package.NormalizedVersion, result[i].Version);
                    PreviewPackages.Verify(x => x.FindPackageByIdAndVersionStrict("Package", Entries[i].Package.NormalizedVersion), Times.Once);
                    Entries[i].Undo.VerifyNoOtherCalls();
                }
                Assert.Equal(" Package ", Requests[0].Id);
                Assert.Equal(" 1.0 ", Requests[0].Version);
                PreviewPackages.VerifyNoOtherCalls();
                Assert.Equal(0, ScopeCount);
                Assert.Equal(0, ContextCount);
            }
        }

        public class TheUndoAsyncMethod : FactsBase
        {
            [Fact]
            public async Task RestoresNormalizedIdentitiesSequentiallyInSeparateScopes()
            {
                Requests[0].Id = " Package ";
                Requests[0].Version = " 1.0 ";
                var firstRestore = new TaskCompletionSource<bool>();
                Entries[0].Undo.Setup(x => x.UndoAsync(Entries[0].Package, Reason, Caller))
                    .Callback(() => Calls.Add("undo 0")).Returns(firstRestore.Task);

                var execution = Target.UndoAsync(Requests, Reason, Caller);

                Assert.False(execution.IsCompleted);
                Assert.Equal(new[] { "find 0", "undo 0" }, Calls);
                Assert.Equal(1, ScopeCount);
                Assert.Equal(1, ContextCount);
                Assert.False(Entries[0].ContextDisposed);
                Assert.False(Entries[0].ScopeEnded);
                firstRestore.SetResult(true);
                var result = await execution;

                AssertOutcomes(result, new Exception[3]);
                Assert.Equal(new[] { "find 0", "undo 0", "find 1", "undo 1", "find 2", "undo 2" }, Calls);
                Assert.Equal(" Package ", Requests[0].Id);
                Assert.Equal(" 1.0 ", Requests[0].Version);
                foreach (var entry in Entries)
                {
                    entry.Packages.Verify(x => x.FindPackageByIdAndVersionStrict("Package", entry.Package.NormalizedVersion), Times.Once);
                    entry.Undo.Verify(x => x.UndoAsync(entry.Package, Reason, Caller), Times.Once);
                    entry.Packages.VerifyNoOtherCalls();
                    entry.Undo.VerifyNoOtherCalls();
                }
                AssertExecuteIsolation();
            }

            [Fact]
            public async Task ContinuesAfterRestoreFailuresAndReturnsEveryOutcomeInOrder()
            {
                var exceptions = new Exception[Entries.Length];
                foreach (var index in new[] { 0, 2 })
                {
                    exceptions[index] = new InvalidOperationException("Restore failed for " + index);
                    Entries[index].Undo.Setup(x => x.UndoAsync(Entries[index].Package, Reason, Caller))
                        .Callback(() => Calls.Add("undo " + index))
                        .ThrowsAsync(exceptions[index]);
                }

                var result = await Target.UndoAsync(Requests, Reason, Caller);

                AssertOutcomes(result, exceptions);
                Assert.Equal(new[] { "find 0", "undo 0", "find 1", "undo 1", "find 2", "undo 2" }, Calls);
                foreach (var entry in Entries)
                {
                    entry.Undo.Verify(x => x.UndoAsync(entry.Package, Reason, Caller), Times.Once);
                }
                AssertExecuteIsolation();
            }

            [Theory]
            [InlineData("missing-package")]
            [InlineData("not-deleted")]
            public async Task RecordsPackageLookupFailuresAndContinues(string failure)
            {
                var entry = Entries[1];
                if (failure == "missing-package")
                {
                    entry.Packages.Setup(x => x.FindPackageByIdAndVersionStrict("Package", "2.0.0"))
                        .Callback(() => Calls.Add("find 1")).Returns((Package)null);
                }
                else
                {
                    entry.Package.PackageStatusKey = PackageStatus.Available;
                }

                var result = await Target.UndoAsync(Requests, Reason, Caller);

                var exception = Assert.IsType<UserSafeException>(result.Results[1].Exception);
                Assert.Contains("Package 2.0.0", exception.Message);
                Assert.Contains(failure == "missing-package" ? "was not found" : "is not soft deleted", exception.Message);
                AssertOutcomes(result, new[] { null, exception, null });
                entry.Undo.Verify(x => x.UndoAsync(It.IsAny<Package>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                Entries[2].Undo.Verify(x => x.UndoAsync(Entries[2].Package, Reason, Caller), Times.Once);
                AssertExecuteIsolation();
            }

            [Theory]
            [InlineData(" \t\r\n")]
            [InlineData("too-long")]
            public async Task RejectsInvalidReasonsBeforeOpeningAnyScopes(string reason)
            {
                if (reason == "too-long")
                {
                    reason = new string('x', 1001);
                }

                var exception = await Assert.ThrowsAsync<UserSafeException>(() => Target.UndoAsync(Requests, reason, Caller));

                Assert.Equal("An undo reason of 1000 characters or fewer is required.", exception.Message);
                AssertNoWork();
            }
        }

        public class SelectionValidation : FactsBase
        {
            [Theory]
            [InlineData("empty")]
            [InlineData("null-item")]
            [InlineData("whitespace-id")]
            [InlineData("bad-version")]
            [InlineData("normalized-duplicate")]
            public async Task RejectsTheWholeStructurallyInvalidSelectionBeforeAnyWork(string invalid)
            {
                IReadOnlyList<UndoPackageRequest> requests = Requests;
                switch (invalid)
                {
                    case "empty": requests = Array.Empty<UndoPackageRequest>(); break;
                    case "null-item": Requests[2] = null; break;
                    case "whitespace-id": Requests[2].Id = " \t"; break;
                    case "bad-version": Requests[2].Version = "not-a-version"; break;
                    case "normalized-duplicate":
                        Requests[0].Version = "1.0";
                        Requests[2] = new UndoPackageRequest { Id = " PACKAGE ", Version = " 1.0.0 " };
                        break;
                }

                await Assert.ThrowsAsync<UserSafeException>(() => Target.PreviewAsync(requests));
                await Assert.ThrowsAsync<UserSafeException>(() => Target.UndoAsync(requests, Reason, Caller));

                AssertNoWork();
            }
        }

        public abstract class FactsBase : IDisposable
        {
            protected const string Reason = "Restore accidentally deleted packages";
            protected const string Caller = "authenticated-admin";
            protected readonly Entry[] Entries;
            protected readonly UndoPackageRequest[] Requests;
            protected readonly List<string> Calls = new List<string>();
            protected readonly Mock<IPackageService> PreviewPackages = new Mock<IPackageService>(MockBehavior.Strict);
            protected readonly IContainer Container;
            protected readonly UndoSoftDeleteBatchService Target;
            protected int ScopeCount;
            protected int ContextCount;

            protected FactsBase()
            {
                Entries = Enumerable.Range(1, 3).Select(i => new Entry
                {
                    Package = new Package
                    {
                        Key = i,
                        PackageRegistration = new PackageRegistration { Id = "Package" },
                        NormalizedVersion = i + ".0.0",
                        PackageStatusKey = PackageStatus.Deleted
                    }
                }).ToArray();
                Requests = Entries.Select(e => new UndoPackageRequest { Id = "Package", Version = e.Package.NormalizedVersion }).ToArray();
                for (var i = 0; i < Entries.Length; i++)
                {
                    var index = i;
                    var entry = Entries[i];
                    entry.Context.Setup(x => x.Dispose()).Callback(() => entry.ContextDisposed = true);
                    PreviewPackages.Setup(x => x.FindPackageByIdAndVersionStrict("Package", entry.Package.NormalizedVersion))
                        .Callback(() => Calls.Add("preview find " + index)).Returns(entry.Package);
                    entry.Packages.Setup(x => x.FindPackageByIdAndVersionStrict("Package", entry.Package.NormalizedVersion))
                        .Callback(() => Calls.Add("find " + index)).Returns(entry.Package);
                    entry.Undo.Setup(x => x.UndoAsync(entry.Package, Reason, Caller))
                        .Callback(() => Calls.Add("undo " + index)).Returns(Task.CompletedTask);
                }

                var builder = new ContainerBuilder();
                builder.Register(c =>
                {
                    // Both services resolve the same context within an entry, but never across entries.
                    if (ContextCount > 0)
                    {
                        Assert.True(Entries[ContextCount - 1].ContextDisposed);
                        Assert.True(Entries[ContextCount - 1].ScopeEnded);
                    }
                    return Entries[ContextCount++];
                }).InstancePerLifetimeScope();
                builder.Register(c => c.Resolve<Entry>().Context.Object).As<IEntitiesContext>().InstancePerLifetimeScope();
                builder.Register(c =>
                {
                    var entry = c.Resolve<Entry>();
                    Assert.Same(entry.Context.Object, c.Resolve<IEntitiesContext>());
                    return entry.Packages.Object;
                }).As<IPackageService>().InstancePerLifetimeScope();
                builder.Register(c =>
                {
                    var entry = c.Resolve<Entry>();
                    Assert.Same(entry.Context.Object, c.Resolve<IEntitiesContext>());
                    return entry.Undo.Object;
                }).As<IUndoSoftDeleteService>().InstancePerLifetimeScope();
                Container = builder.Build();
                Container.ChildLifetimeScopeBeginning += (sender, args) =>
                {
                    var entry = Entries[ScopeCount++];
                    args.LifetimeScope.CurrentScopeEnding += (scopeSender, scopeArgs) => entry.ScopeEnded = true;
                };
                Target = new UndoSoftDeleteBatchService(PreviewPackages.Object, Container);
            }

            protected void AssertOutcomes(UndoSoftDeleteBatchResult result, IReadOnlyList<Exception> exceptions)
            {
                Assert.Equal(Entries.Length, result.Results.Count);
                Assert.Equal(exceptions.All(e => e == null), result.Success);
                Assert.Equal(exceptions.Count(e => e == null), result.RestoredCount);
                for (var i = 0; i < Entries.Length; i++)
                {
                    var outcome = result.Results[i];
                    Assert.NotSame(Requests[i], outcome.Package);
                    Assert.Equal("Package", outcome.Package.Id);
                    Assert.Equal(Entries[i].Package.NormalizedVersion, outcome.Package.Version);
                    Assert.Same(exceptions[i], outcome.Exception);
                    Assert.Equal(exceptions[i] == null, outcome.Success);
                }
            }

            protected void AssertExecuteIsolation()
            {
                Assert.Equal(Entries.Length, ScopeCount);
                Assert.Equal(Entries.Length, ContextCount);
                Assert.Equal(Entries.Length, Entries.Select(e => e.Context.Object).Distinct().Count());
                foreach (var entry in Entries)
                {
                    Assert.True(entry.ContextDisposed);
                    Assert.True(entry.ScopeEnded);
                    entry.Context.Verify(x => x.Dispose(), Times.Once);
                }
                PreviewPackages.VerifyNoOtherCalls();
            }

            protected void AssertNoWork()
            {
                Assert.Empty(Calls);
                Assert.Equal(0, ScopeCount);
                Assert.Equal(0, ContextCount);
                PreviewPackages.VerifyNoOtherCalls();
                foreach (var entry in Entries)
                {
                    entry.Packages.VerifyNoOtherCalls();
                    entry.Undo.VerifyNoOtherCalls();
                }
            }

            public void Dispose()
            {
                Container.Dispose();
            }
        }

        public class Entry
        {
            public Package Package { get; set; }
            public Mock<IPackageService> Packages { get; } = new Mock<IPackageService>(MockBehavior.Strict);
            public Mock<IUndoSoftDeleteService> Undo { get; } = new Mock<IUndoSoftDeleteService>(MockBehavior.Strict);
            public Mock<IEntitiesContext> Context { get; } = new Mock<IEntitiesContext>(MockBehavior.Strict);
            public bool ContextDisposed { get; set; }
            public bool ScopeEnded { get; set; }
        }
    }
}