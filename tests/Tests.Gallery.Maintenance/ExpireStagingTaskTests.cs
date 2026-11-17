// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using Gallery.Maintenance;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery;
using Xunit;

namespace Tests.Gallery.Maintenance
{
    public class ExpireStagingTaskTests
    {
        [Fact]
        public async Task ExpiredGroupDeletesPrivateMembersAndPreservesPublishedContent()
        {
            var context = new TestContext();
            var group = context.AddGroup(1, expired: true);
            var parent = context.AddPackage(1, group);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1);
            var published = context.AddPackage(2, group);
            published.Status = StagedPackageStatus.Succeeded;
            published.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
            var publishedSymbols = context.AddSymbols(published.StagedPackageIdentity, 2);
            publishedSymbols.Status = StagedPackageStatus.Succeeded;
            publishedSymbols.SymbolPackage.StatusKey = PackageStatus.Available;

            await context.RunAsync();

            Assert.Empty(context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Equal(PackageStatus.Deleted, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.False(parent.StagedPackageIdentity.Package.Listed);
            Assert.DoesNotContain(symbols, context.Symbols);
            Assert.DoesNotContain(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.StagingGroupKey);
            Assert.Equal(PackageStatus.Available, published.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Equal(PackageStatus.Available, publishedSymbols.SymbolPackage.StatusKey);
            Assert.Contains(publishedSymbols, context.Symbols);
            Assert.Null(published.StagedPackageIdentity.StagingGroupKey);
            Assert.Contains("staging group Group 1 expired", Assert.Single(context.Notifications).GetSubject());
            context.AssertQueuedFiles(1, packages: 1, symbols: 1);
            context.AssertQueuedFiles(2, packages: 0, symbols: 0);
        }

        [Fact]
        public async Task ExpiredParentRetainsStillLiveSymbolsWithTheirOriginalDeadline()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            var previous = context.AddSymbols(parent.StagedPackageIdentity, 1);
            previous.Status = StagedPackageStatus.Validating;
            var deadline = previous.ExpirationDate;
            previous.WarnedExpirationDate = deadline;

            await context.RunAsync();

            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Equal(PackageStatus.Deleted, parent.StagedPackageIdentity.Package.PackageStatusKey);
            var waiting = parent.StagedPackageIdentity.CurrentStagedSymbolPackage;
            Assert.NotSame(previous, waiting);
            Assert.Equal(StagedPackageStatus.WaitingForParent, waiting.Status);
            Assert.Equal(deadline, waiting.ExpirationDate);
            Assert.Equal(deadline, waiting.WarnedExpirationDate);
            Assert.Contains(previous.SymbolPackage, context.SymbolPackages);
            context.AssertQueuedFiles(1, packages: 1, symbols: 0);
            Assert.Single(context.Notifications);

            await context.RunAsync();

            Assert.Same(waiting, parent.StagedPackageIdentity.CurrentStagedSymbolPackage);
            context.AssertQueuedFiles(1, packages: 1, symbols: 0);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExpiredSymbolsLeaveTheirPublicOrLiveStagedParentUntouched(bool stagedParent)
        {
            var context = new TestContext();
            var parent = context.AddPackage(1);
            if (!stagedParent)
            {
                parent.Status = StagedPackageStatus.Succeeded;
                parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                parent.StagedPackageIdentity.CurrentStagedPackageKey = null;
                parent.StagedPackageIdentity.CurrentStagedPackage = null;
            }
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);
            var originalParentStatus = parent.StagedPackageIdentity.Package.PackageStatusKey;

            await context.RunAsync();

            Assert.Equal(originalParentStatus, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.DoesNotContain(symbols, context.Symbols);
            Assert.DoesNotContain(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            Assert.Equal(stagedParent, context.Identities.Contains(parent.StagedPackageIdentity));
            context.AssertQueuedFiles(1, packages: stagedParent ? 0 : 1, symbols: 1);
            Assert.Contains("staged symbol package Package1 1.0.0 expired", Assert.Single(context.Notifications).GetSubject());
        }

        [Fact]
        public async Task ExpiringBothStandaloneArtifactsRemovesRetainedSymbolsInTheSameRun()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);

            await context.RunAsync();

            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            Assert.Empty(context.Symbols);
            Assert.Empty(context.SymbolPackages);
            Assert.Null(parent.StagedPackageIdentity.CurrentStagedSymbolPackageKey);
            context.AssertQueuedFiles(1, packages: 1, symbols: 2);
            Assert.Equal(2, context.Notifications.Count);
        }

        [Theory]
        [InlineData("group")]
        [InlineData("parent")]
        [InlineData("symbols")]
        [InlineData("group-member", false)]
        [InlineData("group-symbols", false)]
        public async Task AcceptedPromotionRemainsProtectedFromExpirationAndWarnings(string target, bool expired = true)
        {
            var context = new TestContext();
            var grouped = target == "group" || target == "group-member" || target == "group-symbols";
            var group = grouped ? context.AddGroup(1, expired) : null;
            var parent = context.AddPackage(1, group, expired);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired);
            if (!expired)
            {
                parent.ExpirationDate = context.Now.AddHours(12);
                symbols.ExpirationDate = context.Now.AddHours(12);
                if (group != null)
                {
                    group.ExpirationDate = context.Now.AddHours(12);
                }
            }

            if (target == "group")
            {
                group.ActivePromotionId = Guid.NewGuid();
            }
            else if (target == "parent" || target == "group-member")
            {
                parent.Status = StagedPackageStatus.Promoting;
            }
            else
            {
                symbols.Status = StagedPackageStatus.Promoting;
            }

            await context.RunAsync();

            Assert.Equal(PackageStatus.Staged, parent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Contains(symbols, context.Symbols);
            Assert.Contains(symbols.SymbolPackage, context.SymbolPackages);
            Assert.Empty(context.Cleanups);
            Assert.Empty(context.Notifications);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.SaveChangesAsync(), Times.Never));
        }

        [Fact]
        public async Task LiveGroupOverridesExpiredArtifactDeadlinesAndRetiredAttemptsAreIgnored()
        {
            var context = new TestContext();
            var group = context.AddGroup(1);
            var parent = context.AddPackage(1, group, expired: true);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1, expired: true);
            var retired = context.AddPackage(2, expired: true);
            retired.Status = StagedPackageStatus.Superseded;
            retired.StagedPackageIdentity.CurrentStagedPackageKey = 99;

            await context.RunAsync();

            Assert.Contains(group, context.Groups);
            Assert.Equal(StagedPackageStatus.Ready, parent.Status);
            Assert.Contains(symbols, context.Symbols);
            Assert.Equal(StagedPackageStatus.Superseded, retired.Status);
            Assert.Empty(context.Cleanups);
        }

        [Fact]
        public async Task ProtectedGroupsDoNotStarveLaterPages()
        {
            var context = new TestContext();
            for (var key = 1; key <= 100; key++)
            {
                var protectedGroup = context.AddGroup(key, expired: true);
                context.AddPackage(key, protectedGroup).Status = StagedPackageStatus.Promoting;
            }
            var eligible = context.AddGroup(101, expired: true);
            var parent = context.AddPackage(101, eligible);

            await context.RunAsync();

            Assert.Equal(100, context.Groups.Count);
            Assert.DoesNotContain(eligible, context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, parent.Status);
            context.AssertQueuedFiles(101, packages: 1, symbols: 0);
            Assert.Single(context.Cleanups);
        }

        [Fact]
        public async Task RechecksPromotionAtArtifactDeletionAcceptance()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1, expired: true);
            context.BeforeTransaction = () =>
            {
                context.Packages.Remove(parent);
                context.Identities.Remove(parent.StagedPackageIdentity);
                context.AddPackage(1, expired: true).Status = StagedPackageStatus.Promoting;
            };

            await context.RunAsync();

            var current = Assert.Single(context.Packages);
            Assert.NotSame(parent, current);
            Assert.Equal(StagedPackageStatus.Promoting, current.Status);
            Assert.Equal(PackageStatus.Staged, current.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Empty(context.Cleanups);
        }

        [Fact]
        public async Task DisposesEachCandidateContextAcrossPagesAndAllThreeScans()
        {
            var context = new TestContext();
            for (var key = 1; key <= 101; key++)
            {
                context.AddGroup(key, expired: true);
            }
            var expiredParent = context.AddPackage(102, expired: true);
            var liveParent = context.AddPackage(103);
            context.AddSymbols(liveParent.StagedPackageIdentity, 103, expired: true);

            await context.RunAsync();

            Assert.Empty(context.Groups);
            Assert.Equal(StagedPackageStatus.Deleted, expiredParent.Status);
            Assert.Empty(context.Symbols);
            Assert.Equal(PackageStatus.Staged, liveParent.StagedPackageIdentity.Package.PackageStatusKey);
            Assert.Equal(110, context.Contexts.Count);
            Assert.Equal(1, context.MaxActiveContexts);
            Assert.Equal(0, context.ActiveContexts);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.Dispose(), Times.Once));
        }

        [Fact]
        public async Task ConcurrencyConflictDiscardsFailedContextAndContinuesLaterCandidatesAndScans()
        {
            var context = new TestContext();
            var conflicted = context.AddGroup(1, expired: true);
            var laterGroup = context.AddGroup(2, expired: true);
            var groupedParent = context.AddPackage(2, laterGroup);
            var expiredParent = context.AddPackage(3, expired: true);
            var liveParent = context.AddPackage(4);
            context.AddSymbols(liveParent.StagedPackageIdentity, 4, expired: true);
            var exception = new DbUpdateConcurrencyException("The candidate changed during expiration.");
            IEntitiesContext failedContext = null;
            context.BeforeSaveChanges = entities =>
            {
                if (failedContext == null)
                {
                    failedContext = entities;
                    throw exception;
                }
            };

            await context.RunAsync();

            Assert.Same(conflicted, Assert.Single(context.Groups));
            Assert.Equal(StagedPackageStatus.Deleted, groupedParent.Status);
            Assert.Equal(StagedPackageStatus.Deleted, expiredParent.Status);
            Assert.Empty(context.Symbols);
            Assert.Equal(PackageStatus.Staged, liveParent.StagedPackageIdentity.Package.PackageStatusKey);
            Mock.Get(failedContext).Verify(entities => entities.SaveChangesAsync(), Times.Once);
            Mock.Get(failedContext).Verify(entities => entities.Dispose(), Times.Once);
            context.Transactions[failedContext].Verify(transaction => transaction.Commit(), Times.Never);
            context.Transactions[failedContext].Verify(transaction => transaction.Dispose(), Times.Once);
            Assert.Equal(1, context.MaxActiveContexts);
            Assert.Equal(0, context.ActiveContexts);
            var warning = Assert.Single(context.Logger.Invocations.Where(invocation => Equals(invocation.Arguments[0], LogLevel.Warning)));
            Assert.Same(exception, warning.Arguments[3]);
            var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object>>>(warning.Arguments[2]);
            Assert.Contains(state, value => value.Key == "CandidateType" && Equals(value.Value, "staging group"));
            Assert.Contains(state, value => value.Key == "CandidateKey" && Equals(value.Value, conflicted.Key));
        }

        [Fact]
        public async Task OtherDatabaseFailuresAbortRemainingWorkInsteadOfBeingTreatedAsConflicts()
        {
            var context = new TestContext();
            context.AddGroup(1, expired: true);
            var laterGroup = context.AddGroup(2, expired: true);
            var parent = context.AddPackage(3, expired: true);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 3, expired: true);
            var exception = new DbUpdateException("Database failure unrelated to concurrency.");
            context.BeforeSaveChanges = entities => throw exception;

            var actual = await Assert.ThrowsAsync<DbUpdateException>(context.RunAsync);

            Assert.Same(exception, actual);
            Assert.Contains(laterGroup, context.Groups);
            Assert.Equal(StagedPackageStatus.Ready, parent.Status);
            Assert.Contains(symbols, context.Symbols);
            Assert.Empty(context.Cleanups);
            Assert.Equal(0, context.ActiveContexts);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.Dispose(), Times.Once));
            Assert.DoesNotContain(context.Logger.Invocations, invocation => Equals(invocation.Arguments[0], LogLevel.Warning));
            Assert.Empty(context.Notifications);
        }

        [Theory]
        [InlineData("group")]
        [InlineData("parent")]
        [InlineData("symbols")]
        public async Task WarnsAtTwentyFourHoursOncePerDeadlineAndNotifiesAfterAutomaticDeletion(string target)
        {
            var context = new TestContext();
            var group = target == "group" ? context.AddGroup(1) : null;
            var parent = context.AddPackage(1, group);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 1);
            Action<DateTime> setDeadline;
            Func<DateTime?> getWarnedDeadline;
            if (group != null)
            {
                setDeadline = value => group.ExpirationDate = value;
                getWarnedDeadline = () => group.WarnedExpirationDate;
            }
            else if (target == "parent")
            {
                setDeadline = value => parent.ExpirationDate = value;
                getWarnedDeadline = () => parent.WarnedExpirationDate;
            }
            else
            {
                parent.StagedPackageIdentity.Package.PackageStatusKey = PackageStatus.Available;
                parent.StagedPackageIdentity.CurrentStagedPackageKey = null;
                parent.StagedPackageIdentity.CurrentStagedPackage = null;
                setDeadline = value => symbols.ExpirationDate = value;
                getWarnedDeadline = () => symbols.WarnedExpirationDate;
            }

            setDeadline(context.Now.AddDays(1).AddTicks(1));
            await context.RunAsync();
            Assert.Empty(context.Notifications);
            Assert.Null(getWarnedDeadline());

            setDeadline(context.Now.AddDays(1));
            await context.RunAsync();
            Assert.Contains("expires soon", Assert.Single(context.Notifications).GetSubject());
            Assert.Equal(context.Now.AddDays(1), getWarnedDeadline());
            Assert.Empty(context.Cleanups);
            await context.RunAsync();
            Assert.Single(context.Notifications);

            setDeadline(context.Now.AddHours(23));
            await context.RunAsync();
            Assert.Equal(2, context.Notifications.Count);
            Assert.Equal(context.Now.AddHours(23), getWarnedDeadline());

            setDeadline(context.Now);
            await context.RunAsync();
            Assert.Equal(3, context.Notifications.Count);
            Assert.Contains("expired", context.Notifications.Last().GetSubject());
            Assert.NotEmpty(context.Cleanups);
            await context.RunAsync();
            Assert.Equal(3, context.Notifications.Count);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task OptedOutAndDeletedOwnersReceiveNothingButExpirationStillDeletesContent(bool subscribed, bool deleted)
        {
            var context = new TestContext();
            context.Owner.NotifyPackageStaged = subscribed;
            context.Owner.IsDeleted = deleted;
            var group = context.AddGroup(1);
            context.AddPackage(1, group);
            var parent = context.AddPackage(2);
            var symbols = context.AddSymbols(parent.StagedPackageIdentity, 2);
            group.ExpirationDate = context.Now.AddHours(12);
            parent.ExpirationDate = context.Now.AddHours(12);
            symbols.ExpirationDate = context.Now.AddHours(12);

            await context.RunAsync();

            Assert.Empty(context.Notifications);
            Assert.Null(group.WarnedExpirationDate);
            Assert.Null(parent.WarnedExpirationDate);
            Assert.Null(symbols.WarnedExpirationDate);

            group.ExpirationDate = context.Now;
            parent.ExpirationDate = context.Now;
            symbols.ExpirationDate = context.Now;
            await context.RunAsync();

            Assert.Empty(context.Groups);
            Assert.All(context.Packages, attempt => Assert.Equal(StagedPackageStatus.Deleted, attempt.Status));
            Assert.Empty(context.Symbols);
            Assert.Empty(context.Notifications);
        }

        [Fact]
        public async Task WarningRechecksRefreshedDeadlineBeforeSavingOrSending()
        {
            var context = new TestContext();
            var parent = context.AddPackage(1);
            parent.ExpirationDate = context.Now.AddHours(12);
            context.BeforeTransaction = () => parent.ExpirationDate = context.Now.AddDays(2);

            await context.RunAsync();

            Assert.Null(parent.WarnedExpirationDate);
            Assert.Empty(context.Notifications);
            Assert.All(context.Contexts, entities => entities.Verify(value => value.SaveChangesAsync(), Times.Never));
        }

        private class TestContext
        {
            public TestContext()
            {
                Messages.Setup(service => service.SendMessageAsync(It.IsAny<IEmailBuilder>(), false, false))
                    .Callback<IEmailBuilder, bool, bool>((message, copySender, discloseSender) =>
                    {
                        Transactions[Contexts.Last().Object].Verify(transaction => transaction.Commit(), Times.Once);
                        if (message.GetRecipients().To.Any())
                        {
                            Notifications.Add(message);
                        }
                    }).Returns(Task.CompletedTask);
            }

            public DateTime Now { get; } = DateTime.UtcNow;

            public User Owner { get; } = new User("owner") { Key = 1, EmailAddress = "owner@example.test", NotifyPackageStaged = true };

            public Mock<IMessageService> Messages { get; } = new Mock<IMessageService>();

            public List<IEmailBuilder> Notifications { get; } = new List<IEmailBuilder>();

            public List<StagingGroup> Groups { get; } = new List<StagingGroup>();

            public List<StagedPackage> Packages { get; } = new List<StagedPackage>();

            public List<StagedSymbolPackage> Symbols { get; } = new List<StagedSymbolPackage>();

            public List<StagedPackageIdentity> Identities { get; } = new List<StagedPackageIdentity>();

            public List<SymbolPackage> SymbolPackages { get; } = new List<SymbolPackage>();

            public List<StagingBlobCleanup> Cleanups { get; } = new List<StagingBlobCleanup>();

            public List<Mock<IEntitiesContext>> Contexts { get; } = new List<Mock<IEntitiesContext>>();

            public Dictionary<IEntitiesContext, Mock<IDbContextTransaction>> Transactions { get; } = new Dictionary<IEntitiesContext, Mock<IDbContextTransaction>>();

            public Mock<ILogger<ExpireStagingTask>> Logger { get; } = new Mock<ILogger<ExpireStagingTask>>();

            public Action BeforeTransaction { get; set; }

            public Action<IEntitiesContext> BeforeSaveChanges { get; set; }

            public int ActiveContexts { get; private set; }

            public int MaxActiveContexts { get; private set; }

            public StagingGroup AddGroup(int key, bool expired = false)
            {
                var group = new StagingGroup { Key = key, OwnerKey = 1, Owner = Owner, Id = $"group-{key}", Name = $"Group {key}", ExpirationDate = Now.AddDays(expired ? -1 : 2) };
                Groups.Add(group);
                return group;
            }

            public StagedPackage AddPackage(int key, StagingGroup group = null, bool expired = false)
            {
                var identity = new StagedPackageIdentity
                {
                    Key = key,
                    OwnerKey = 1,
                    Owner = Owner,
                    StagingGroupKey = group?.Key,
                    StagingGroup = group,
                    Package = new Package { Key = key, PackageRegistration = new PackageRegistration { Id = $"Package{key}" }, NormalizedVersion = "1.0.0", PackageStatusKey = PackageStatus.Staged, Listed = true },
                    CurrentStagedPackageKey = key,
                };
                var attempt = new StagedPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = key,
                    StagedPackageIdentity = identity,
                    Status = StagedPackageStatus.Ready,
                    ExpirationDate = Now.AddDays(expired ? -1 : 2),
                    UploadedBlobPath = $"parent-{key}.nupkg",
                    UploadedBlobETag = $"parent-etag-{key}",
                };
                identity.CurrentStagedPackage = attempt;
                Identities.Add(identity);
                Packages.Add(attempt);
                return attempt;
            }

            public StagedSymbolPackage AddSymbols(StagedPackageIdentity identity, int key, bool expired = false)
            {
                var symbols = new SymbolPackage { Key = key, PackageKey = identity.Key, Package = identity.Package, StatusKey = PackageStatus.Staged };
                var attempt = new StagedSymbolPackage
                {
                    Key = key,
                    StagedPackageIdentityKey = identity.Key,
                    StagedPackageIdentity = identity,
                    SymbolPackage = symbols,
                    Status = StagedPackageStatus.Ready,
                    ExpirationDate = Now.AddDays(expired ? -1 : 2),
                    UploadedBlobPath = $"symbols-{key}.snupkg",
                    UploadedBlobETag = $"etag-{key}",
                };
                identity.CurrentStagedSymbolPackageKey = key;
                identity.CurrentStagedSymbolPackage = attempt;
                Symbols.Add(attempt);
                SymbolPackages.Add(symbols);
                return attempt;
            }

            public Task RunAsync()
            {
                return new ExpireStagingTask(Logger.Object)
                    .ProcessAsync(CreateContextAsync, Messages.Object, new MaintenanceEmailConfiguration
                    {
                        ManagePackagesUrl = "https://gallery.test/account/Packages",
                        EmailSettingsUrl = "https://gallery.test/account",
                    }, Now);
            }

            public void AssertQueuedFiles(int identityKey, int packages, int symbols)
            {
                Assert.Equal(packages, Cleanups.Count(request => request.StagedPackageIdentityKey == identityKey && request.BlobPath.EndsWith(".nupkg", StringComparison.Ordinal)));
                Assert.Equal(symbols, Cleanups.Count(request => request.StagedPackageIdentityKey == identityKey && request.BlobPath.EndsWith(".snupkg", StringComparison.Ordinal)));
            }

            private Task<IEntitiesContext> CreateContextAsync()
            {
                var entities = new Mock<IEntitiesContext>();
                var pendingDeletes = new List<Action>();
                var groups = CreateSet(Groups, pendingDeletes);
                var packages = CreateSet(Packages, pendingDeletes);
                var symbols = CreateSet(Symbols, pendingDeletes);
                var identities = CreateSet(Identities, pendingDeletes);
                var symbolPackages = CreateSet(SymbolPackages, pendingDeletes);
                var cleanups = CreateSet(Cleanups, pendingDeletes);
                entities.Setup(value => value.StagingGroups).Returns(groups.Object);
                entities.Setup(value => value.StagedPackages).Returns(packages.Object);
                entities.Setup(value => value.StagedSymbolPackages).Returns(symbols.Object);
                entities.Setup(value => value.Set<StagingGroup>()).Returns(groups.Object);
                entities.Setup(value => value.Set<StagedPackage>()).Returns(packages.Object);
                entities.Setup(value => value.Set<StagedSymbolPackage>()).Returns(symbols.Object);
                entities.Setup(value => value.Set<StagedPackageIdentity>()).Returns(identities.Object);
                entities.Setup(value => value.Set<SymbolPackage>()).Returns(symbolPackages.Object);
                entities.Setup(value => value.Set<StagingBlobCleanup>()).Returns(cleanups.Object);
                symbols.Setup(value => value.Add(It.IsAny<StagedSymbolPackage>()))
                    .Callback<StagedSymbolPackage>(attempt =>
                    {
                        attempt.Key = Symbols.Max(symbol => symbol.Key) + 1;
                        attempt.StagedPackageIdentityKey = attempt.StagedPackageIdentity.Key;
                        Symbols.Add(attempt);
                    }).Returns<StagedSymbolPackage>(attempt => attempt);
                entities.Setup(value => value.SaveChangesAsync()).Callback(() =>
                {
                    BeforeSaveChanges?.Invoke(entities.Object);
                    foreach (var delete in pendingDeletes)
                    {
                        delete();
                    }
                    pendingDeletes.Clear();
                }).ReturnsAsync(1);
                var transaction = new Mock<IDbContextTransaction>();
                Transactions.Add(entities.Object, transaction);
                var database = new Mock<IDatabase>();
                database.Setup(value => value.BeginTransaction()).Callback(() => BeforeTransaction?.Invoke())
                    .Returns(transaction.Object);
                entities.Setup(value => value.GetDatabase()).Returns(database.Object);
                ActiveContexts++;
                MaxActiveContexts = Math.Max(MaxActiveContexts, ActiveContexts);
                entities.Setup(value => value.Dispose()).Callback(() => ActiveContexts--);
                Contexts.Add(entities);
                return Task.FromResult(entities.Object);
            }

            private static Mock<DbSet<T>> CreateSet<T>(List<T> items, List<Action> pendingDeletes) where T : class
            {
                var set = new Mock<DbSet<T>>().SetupDbSet(items);
                set.Setup(value => value.Include(It.IsAny<string>())).Returns(set.Object);
                set.Setup(value => value.AsNoTracking()).Returns(set.Object);
                set.Setup(value => value.Add(It.IsAny<T>())).Callback<T>(items.Add).Returns<T>(item => item);
                set.Setup(value => value.Remove(It.IsAny<T>())).Callback<T>(item => pendingDeletes.Add(() => items.Remove(item))).Returns<T>(item => item);
                return set;
            }
        }
    }
}
