// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery
{
    public class StagingServiceManagementFacts
    {
        public class TheGetPackageMethod
        {
            [Fact]
            public void ReturnsResourceForOwnedPackage()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0");

                var entry = harness.Target.GetPackage(harness.Context, "packagea", "1.0.0");

                Assert.Equal("PackageA", entry.Id);
                Assert.Equal("1.0.0", entry.Version);
                Assert.Equal(harness.Owner.Username, entry.Owner);
                Assert.Equal(StagingResourceValues.StatusReady, entry.Status);
                Assert.True(entry.CanPromote);
                Assert.Empty(entry.Blockers);
                Assert.StartsWith("https://nuget.test/account/packages/staging", entry.GalleryUrl);
            }

            [Fact]
            public void HidesAnotherOwnersPackageWithNotFound()
            {
                var harness = new Harness();
                var other = new User { Key = 99, Username = "other" };
                harness.AddStagedEntry("PackageA", "1.0.0", owner: other);

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0"));

                Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public void ReturnsNotFoundForUnparseableVersion()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0");

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetPackage(harness.Context, "PackageA", "not-a-version"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public void OmitsErrorDetailsForFailedArtifactOnFullRead()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", packageStatus: StagingArtifactStatus.ValidationFailed);

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusValidationFailed, resource.Status);

                // Artifact validation-error details are deferred for Unit 9: errors are omitted (null), not an empty array.
                Assert.Null(resource.Package.Errors);
            }
        }

        public class TheStagedPackageStatusDerivation
        {
            [Fact]
            public void GroupedReadyEntryStaysReadyButCannotPromote()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.AddStagedEntry("PackageA", "1.0.0", group: group);

                var entry = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusReady, entry.Status);
                Assert.False(entry.CanPromote);
                Assert.Contains(entry.Blockers, x => x.Code == StagingBlockerCodes.GroupedPackage);
            }

            [Fact]
            public void SymbolValidationFailureSurfacesBlockerAndStatus()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.ValidationFailed);

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusValidationFailed, resource.Status);
                Assert.Contains(resource.Blockers, x => x.Code == StagingBlockerCodes.SymbolValidationFailed && x.Artifact == "symbols");
            }

            [Fact]
            public void SymbolsOnlyEntryWithDeletedParentIsBlocked()
            {
                var harness = new Harness();
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.Deleted);

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusBlocked, resource.Status);
                Assert.Equal(StagingResourceValues.SourceReference, resource.Package.Source);
                Assert.Contains(resource.Blockers, x => x.Code == StagingBlockerCodes.ParentDeleted);
            }

            [Fact]
            public void OwnershipRemovalBlocksPromotion()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                entry.Package.PackageRegistration.Owners.Clear();
                entry.Package.PackageRegistration.Owners.Add(new User { Key = 777 });

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusBlocked, resource.Status);
                Assert.Contains(resource.Blockers, x => x.Code == StagingBlockerCodes.OwnershipRemoved);
            }

            [Fact]
            public void OwnerlessRegistrationBlocksPromotion()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                entry.Package.PackageRegistration.Owners.Clear();

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusBlocked, resource.Status);
                Assert.Contains(resource.Blockers, x => x.Code == StagingBlockerCodes.OwnershipRemoved);
            }

            [Fact]
            public void PromotingArtifactReportsPromotingStatusStartedAndRecoveryUrl()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", packageStatus: StagingArtifactStatus.Promoting);
                var promotionId = Guid.NewGuid();
                harness.AttachPromotion(entry.PackageArtifact, promotionId, harness.Now);

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusPromoting, resource.Status);
                Assert.NotNull(resource.Promotion);
                Assert.Equal(StagingResourceValues.StatusPromoting, resource.Promotion.Status);
                Assert.Equal(harness.Now, resource.Promotion.Started);
                Assert.EndsWith("/promotions/" + promotionId.ToString("D"), resource.Promotion.GalleryUrl);
            }

            [Fact]
            public void PromotingArtifactWithoutLoadedHistoryOmitsRecoveryUrlAndStarted()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", packageStatus: StagingArtifactStatus.Promoting);
                entry.PackageArtifact.PromotionArtifactHistoryKey = 7;

                var resource = harness.Target.GetPackage(harness.Context, "PackageA", "1.0.0");

                Assert.Equal(StagingResourceValues.StatusPromoting, resource.Status);
                Assert.NotNull(resource.Promotion);
                Assert.Null(resource.Promotion.GalleryUrl);
                Assert.Null(resource.Promotion.Started);
            }
        }

        public class TheListPackagesMethod
        {
            [Fact]
            public void ReturnsPackagesWithQuota()
            {
                var harness = new Harness();
                harness.Owner.StagingArtifactLimit = 350;
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                harness.AddStagedEntry("PackageB", "1.0.0", key: 2);

                var list = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 100, continuationToken: null);

                Assert.Equal(2, list.Packages.Count);
                Assert.Equal(2, list.Quota.UsedArtifacts);
                Assert.Equal(350, list.Quota.Limit);
                Assert.Null(list.ContinuationToken);
            }

            [Fact]
            public void OmitsValidationErrorDetailsInCompactList()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1, packageStatus: StagingArtifactStatus.ValidationFailed);

                var list = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 100, continuationToken: null);

                Assert.Null(list.Packages.Single().Package.Errors);
            }

            [Fact]
            public void RejectsBothFilters()
            {
                var harness = new Harness();

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: "net10", ungrouped: true, take: 100, continuationToken: null));

                Assert.Equal(StagingApiErrorCodes.InvalidFilterCombination, exception.Code);
            }

            [Fact]
            public void ReturnsGroupNotFoundForUnknownFilter()
            {
                var harness = new Harness();

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: "missing", ungrouped: false, take: 100, continuationToken: null));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }

            [Fact]
            public void PagesWithStableCursor()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                harness.AddStagedEntry("PackageB", "1.0.0", key: 2);
                harness.AddStagedEntry("PackageC", "1.0.0", key: 3);

                var firstPage = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 2, continuationToken: null);
                Assert.Equal(2, firstPage.Packages.Count);
                Assert.NotNull(firstPage.ContinuationToken);

                var secondPage = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 2, continuationToken: firstPage.ContinuationToken);
                Assert.Single(secondPage.Packages);
                Assert.Null(secondPage.ContinuationToken);
                Assert.Equal("PackageC", secondPage.Packages.Single().Id);
            }

            [Fact]
            public void RejectsTokenReusedWithDifferentFilter()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                harness.AddStagedEntry("PackageB", "1.0.0", key: 2);

                var firstPage = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 1, continuationToken: null);

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: true, take: 1, continuationToken: firstPage.ContinuationToken));

                Assert.Equal(StagingApiErrorCodes.InvalidContinuationToken, exception.Code);
            }

            [Fact]
            public void PagesTwoThousandPackagesForNarrowPatternWithoutDuplicatesOrOmissions()
            {
                var harness = new Harness();
                var expectedVisible = new SortedSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < 2000; i++)
                {
                    var visible = i % 2 == 0;
                    var id = (visible ? "Visible." : "Hidden.") + i.ToString("D4");
                    harness.AddStagedEntry(id, "1.0.0", key: i + 1);
                    if (visible)
                    {
                        expectedVisible.Add(id);
                    }
                }

                var context = harness.ContextForSubjects("Visible.*");
                var seen = new List<string>();
                string token = null;
                var pageCount = 0;
                do
                {
                    var page = harness.Target.ListPackages(context, groupId: null, ungrouped: false, take: 100, continuationToken: token);
                    Assert.True(page.Packages.Count <= 100);
                    seen.AddRange(page.Packages.Select(x => x.Id));
                    token = page.ContinuationToken;
                    pageCount++;
                    Assert.True(pageCount <= 25, "Pagination did not terminate.");
                }
                while (token != null);

                Assert.All(seen, id => Assert.StartsWith("Visible.", id));
                Assert.Equal(seen.Count, seen.Distinct().Count());
                Assert.Equal(expectedVisible.Count, seen.Count);
                Assert.Equal(expectedVisible.ToList(), seen.OrderBy(x => x, StringComparer.Ordinal).ToList());
            }

            [Fact]
            public void RejectsMalformedToken()
            {
                var harness = new Harness();

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 100, continuationToken: "not-base64!!"));

                Assert.Equal(StagingApiErrorCodes.InvalidContinuationToken, exception.Code);
            }

            [Fact]
            public void RejectsTamperedToken()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                harness.AddStagedEntry("PackageB", "1.0.0", key: 2);
                var firstPage = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 1, continuationToken: null);
                var tampered = FlipLastCharacter(firstPage.ContinuationToken);

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 1, continuationToken: tampered));

                Assert.Equal(StagingApiErrorCodes.InvalidContinuationToken, exception.Code);
            }

            [Fact]
            public void RejectsForgedPlainBase64Token()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"o\":1,\"f\":\"all\",\"k\":0000000000000000000000000000000000000000}"));

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 1, continuationToken: forged));

                Assert.Equal(StagingApiErrorCodes.InvalidContinuationToken, exception.Code);
            }

            [Fact]
            public void RejectsTokenReusedWithDifferentOwner()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0", key: 1);
                harness.AddStagedEntry("PackageB", "1.0.0", key: 2);
                var firstPage = harness.Target.ListPackages(harness.Context, groupId: null, ungrouped: false, take: 1, continuationToken: null);
                var otherOwner = new User { Key = 424242, Username = "intruder" };
                var otherContext = harness.ContextForOwner(otherOwner);

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(otherContext, groupId: null, ungrouped: false, take: 1, continuationToken: firstPage.ContinuationToken));

                Assert.Equal(StagingApiErrorCodes.InvalidContinuationToken, exception.Code);
            }

            private static string FlipLastCharacter(string token)
            {
                var chars = token.ToCharArray();
                var last = chars.Length - 1;
                chars[last] = chars[last] == 'A' ? 'B' : 'A';
                return new string(chars);
            }
        }

        public class TheDownloadMethods
        {
            [Fact]
            public async Task StreamsExactStagedNupkgWithoutExposingStorage()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                var content = new MemoryStream(Encoding.UTF8.GetBytes("nupkg-bytes"));
                harness.BlobService
                    .Setup(x => x.OpenReadAsync(It.Is<StagingBlobReference>(r => r.BlobPath == entry.PackageArtifact.BlobPath && r.ETag == entry.PackageArtifact.BlobETag)))
                    .ReturnsAsync(content);

                var download = await harness.Target.DownloadPackageAsync(harness.Context, "PackageA", "1.0.0");
                using (download.Content)
                {
                    Assert.Equal("PackageA.1.0.0.nupkg", download.FileName);
                    Assert.Same(content, download.Content);
                }
            }

            [Fact]
            public async Task ReturnsNotFoundWhenNoStagedNupkg()
            {
                var harness = new Harness();
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.Available);

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DownloadPackageAsync(harness.Context, "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public async Task StreamsStagedSnupkg()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.Ready);
                var content = new MemoryStream(Encoding.UTF8.GetBytes("snupkg-bytes"));
                harness.BlobService
                    .Setup(x => x.OpenReadAsync(It.Is<StagingBlobReference>(r => r.BlobType == StagingBlobType.Snupkg)))
                    .ReturnsAsync(content);

                var download = await harness.Target.DownloadSymbolsAsync(harness.Context, "PackageA", "1.0.0");
                using (download.Content)
                {
                    Assert.Equal("PackageA.1.0.0.snupkg", download.FileName);
                }
            }
        }

        public class TheSetListedMethod
        {
            [Fact]
            public async Task ChangesListedIntentWithoutRevalidating()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                entry.Package.Listed = true;

                var resource = await harness.Target.SetListedAsync(harness.Context, "PackageA", "1.0.0", listed: false);

                Assert.False(entry.Package.Listed);
                Assert.Equal(StagingArtifactStatus.Ready, entry.PackageArtifact.Status);
                harness.Transaction.Verify(x => x.Commit(), Times.Once);
                harness.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task RejectsSymbolsOnlyEntry()
            {
                var harness = new Harness();
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.Available);

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.SetListedAsync(harness.Context, "PackageA", "1.0.0", listed: false));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public async Task RejectsPromotingPackage()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", packageStatus: StagingArtifactStatus.Promoting);
                entry.PackageArtifact.PromotionArtifactHistoryKey = 9;

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.SetListedAsync(harness.Context, "PackageA", "1.0.0", listed: false));

                Assert.Equal(StagingApiErrorCodes.StagedPackagePromoting, exception.Code);
            }
        }

        public class TheDeletePackageMethod
        {
            [Fact]
            public async Task RemovesArtifactsAndQueuesBlobsAndHardDeletesStagedPackage()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.Ready);

                await harness.Target.DeletePackageAsync(harness.Context, "PackageA", "1.0.0");

                Assert.Empty(harness.Entities.StagingEntries);
                Assert.Empty(harness.Entities.StagedPackageArtifacts);
                Assert.Empty(harness.Entities.StagedSymbolArtifacts);
                Assert.Empty(harness.Entities.SymbolPackages);
                Assert.Empty(harness.Entities.Packages);
                Assert.Equal(2, harness.Entities.StagingBlobCleanups.Count());
                harness.Transaction.Verify(x => x.Commit(), Times.Once);
            }

            [Fact]
            public async Task LeavesReferencedParentForSymbolsOnlyPackage()
            {
                var harness = new Harness();
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.Available);

                await harness.Target.DeletePackageAsync(harness.Context, "PackageA", "1.0.0");

                Assert.Empty(harness.Entities.StagingEntries);
                Assert.Empty(harness.Entities.StagedSymbolArtifacts);
                Assert.Single(harness.Entities.Packages);
                Assert.Single(harness.Entities.StagingBlobCleanups);
            }

            [Fact]
            public async Task RejectsPromotingPackage()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", packageStatus: StagingArtifactStatus.Promoting);
                entry.PackageArtifact.PromotionArtifactHistoryKey = 3;

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeletePackageAsync(harness.Context, "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackagePromoting, exception.Code);
                Assert.Single(harness.Entities.StagingEntries);
            }

            [Fact]
            public async Task ReturnsNotFoundForMissingPackage()
            {
                var harness = new Harness();

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeletePackageAsync(harness.Context, "Missing", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }
        }

        public class TheDeleteSymbolsMethod
        {
            [Fact]
            public async Task RemovesSymbolsButKeepsPackageEntry()
            {
                var harness = new Harness();
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.Ready);

                await harness.Target.DeleteSymbolsAsync(harness.Context, "PackageA", "1.0.0");

                Assert.Single(harness.Entities.StagingEntries);
                Assert.Empty(harness.Entities.StagedSymbolArtifacts);
                Assert.Single(harness.Entities.StagedPackageArtifacts);
                Assert.Single(harness.Entities.StagingBlobCleanups);
            }

            [Fact]
            public async Task DeletesEmptyEntryWhenSymbolsWereLastArtifact()
            {
                var harness = new Harness();
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.Available);

                await harness.Target.DeleteSymbolsAsync(harness.Context, "PackageA", "1.0.0");

                Assert.Empty(harness.Entities.StagingEntries);
                Assert.Single(harness.Entities.Packages);
            }

            [Fact]
            public async Task ReturnsNotFoundWhenNoSymbols()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeleteSymbolsAsync(harness.Context, "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }
        }

        public class TheGroupMethods
        {
            [Fact]
            public async Task CreateGroupCreatesNewGroup()
            {
                var harness = new Harness();

                var result = await harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "net10-preview", Name = ".NET 10 Preview" });

                Assert.True(result.Created);
                Assert.Equal("net10-preview", result.Group.Id);
                Assert.Equal(".NET 10 Preview", result.Group.Name);
                Assert.Single(harness.Entities.StagingGroups);
            }

            [Fact]
            public async Task CreateGroupIsIdempotentForExistingId()
            {
                var harness = new Harness();
                harness.AddGroup("net10-preview");

                var result = await harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "net10-preview" });

                Assert.False(result.Created);
                Assert.Single(harness.Entities.StagingGroups);
            }

            [Fact]
            public async Task CreateGroupIsIdempotentWhenRequestedNameMatchesStoredName()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10-preview");
                group.Name = ".NET 10 Preview";

                var result = await harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "net10-preview", Name = ".NET 10 Preview" });

                Assert.False(result.Created);
                Assert.Single(harness.Entities.StagingGroups);
            }

            [Fact]
            public async Task CreateGroupWithDifferentNameReturnsConflict()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10-preview");
                group.Name = "Original";

                var exception = await Assert.ThrowsAsync<StagingApiException>(() =>
                    harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "net10-preview", Name = "Different" }));

                Assert.Equal(System.Net.HttpStatusCode.Conflict, exception.StatusCode);
                Assert.Equal(StagingApiErrorCodes.GroupAlreadyExists, exception.Code);
                Assert.Equal("Original", harness.Entities.StagingGroups.Single().Name);
            }

            [Fact]
            public async Task CreateGroupRejectsInvalidId()
            {
                var harness = new Harness();

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "Invalid Id" }));

                Assert.Equal(StagingApiErrorCodes.InvalidGroupId, exception.Code);
            }

            [Fact]
            public async Task CreateGroupEnforcesGroupLimit()
            {
                var harness = new Harness();
                for (var i = 0; i < StagingService.DefaultGroupLimit; i++)
                {
                    harness.AddGroup("group-" + i);
                }

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "one-too-many" }));

                Assert.Equal(StagingApiErrorCodes.GroupLimitExceeded, exception.Code);
            }

            [Fact]
            public void GroupSummaryAtScaleCountsVisibleMembersAndAppliesPrecedence()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var key = 1;
                for (var i = 0; i < 300; i++)
                {
                    harness.AddStagedEntry("Visible.Ready." + i.ToString("D4"), "1.0.0", key: key++, group: group);
                }

                var failed = harness.AddStagedEntry("Visible.Failed.0001", "1.0.0", key: key++, group: group);
                harness.AddSymbolArtifact(failed, StagingArtifactStatus.ValidationFailed);

                for (var i = 0; i < 50; i++)
                {
                    harness.AddStagedEntry("Hidden.Promoting." + i.ToString("D4"), "1.0.0", key: key++, group: group, packageStatus: StagingArtifactStatus.Promoting);
                }

                var context = harness.ContextForSubjects("Visible.*");

                var summary = Assert.Single(harness.Target.ListGroups(context));

                Assert.Equal(301, summary.PackageCount);
                Assert.Equal(302, summary.ArtifactCount);

                // Precedence is applied only over visible members: the hidden promoting entries must not win, and the
                // single visible failed symbol yields validationFailed.
                Assert.Equal(StagingResourceValues.StatusValidationFailed, summary.Status);
                Assert.False(summary.CanPromote);
            }

            [Fact]
            public void ListGroupsReturnsSummariesWithCounts()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", group: group);
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.Ready);

                var groups = harness.Target.ListGroups(harness.Context);

                var summary = Assert.Single(groups);
                Assert.Equal("net10", summary.Id);
                Assert.Equal(1, summary.PackageCount);                Assert.Equal(2, summary.ArtifactCount);
                Assert.Equal(StagingResourceValues.StatusReady, summary.Status);
                Assert.True(summary.CanPromote);
            }

            [Fact]
            public void GetGroupReturnsDetailWithEntries()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.AddStagedEntry("PackageA", "1.0.0", group: group, key: 1);

                var detail = harness.Target.GetGroup(harness.Context, "net10", take: 100, continuationToken: null);

                Assert.Equal("net10", detail.Id);
                Assert.Equal(1, detail.PackageCount);
                Assert.Single(detail.Packages);
            }

            [Fact]
            public void GetGroupReturnsNotFoundForMissingGroup()
            {
                var harness = new Harness();

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetGroup(harness.Context, "missing", take: 100, continuationToken: null));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }

            [Fact]
            public async Task RenameGroupUpdatesName()
            {
                var harness = new Harness();
                harness.AddGroup("net10");

                var summary = await harness.Target.RenameGroupAsync(harness.Context, "net10", new StagingRenameGroupRequest { Name = "Renamed" });

                Assert.Equal("Renamed", summary.Name);
                Assert.Equal("Renamed", harness.Entities.StagingGroups.Single().Name);
            }

            [Fact]
            public async Task DeleteGroupRemovesEntriesArtifactsAndGroup()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", group: group);
                harness.AddSymbolArtifact(entry, StagingArtifactStatus.Ready);

                await harness.Target.DeleteGroupAsync(harness.Context, "net10");

                Assert.Empty(harness.Entities.StagingGroups);
                Assert.Empty(harness.Entities.StagingEntries);
                Assert.Empty(harness.Entities.StagedPackageArtifacts);
                Assert.Empty(harness.Entities.StagedSymbolArtifacts);
                Assert.Equal(2, harness.Entities.StagingBlobCleanups.Count());
            }

            [Fact]
            public async Task RenameGroupRejectsMissingName()
            {
                var harness = new Harness();
                harness.AddGroup("net10");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.RenameGroupAsync(harness.Context, "net10", new StagingRenameGroupRequest()));

                Assert.Equal(StagingApiErrorCodes.InvalidRequestBody, exception.Code);
                Assert.Equal("net10", harness.Entities.StagingGroups.Single().Name);
            }

            [Fact]
            public async Task RenameGroupRejectsActivePromotion()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.Entities.StagingPromotionHistories.Add(new StagingPromotionHistory { GroupKey = group.Key, Status = StagingPromotionHistoryStatus.InProgress });

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.RenameGroupAsync(harness.Context, "net10", new StagingRenameGroupRequest { Name = "Renamed" }));

                Assert.Equal(StagingApiErrorCodes.GroupPromoting, exception.Code);
                Assert.Equal("net10", harness.Entities.StagingGroups.Single().Name);
            }

            [Fact]
            public async Task CreateGroupDefaultsMissingNameToId()
            {
                var harness = new Harness();

                var result = await harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest { Id = "net10-preview" });

                Assert.Equal("net10-preview", result.Group.Name);
            }

            [Fact]
            public async Task CreateGroupRejectsMissingId()
            {
                var harness = new Harness();

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.CreateGroupAsync(harness.Context, new StagingCreateGroupRequest()));

                Assert.Equal(StagingApiErrorCodes.InvalidGroupId, exception.Code);
            }

            [Fact]
            public void GroupSummaryReportsBlockedStatusForOwnershipRemoval()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", group: group);
                entry.Package.PackageRegistration.Owners.Clear();
                entry.Package.PackageRegistration.Owners.Add(new User { Key = 555 });

                var summary = Assert.Single(harness.Target.ListGroups(harness.Context));

                Assert.Equal(StagingResourceValues.StatusBlocked, summary.Status);
                Assert.False(summary.CanPromote);
                Assert.Equal(1, summary.PackageCount);
                Assert.Equal(1, summary.ArtifactCount);
            }

            [Fact]
            public void GroupSummaryReportsBlockedStatusForOwnerlessRegistration()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", group: group);
                entry.Package.PackageRegistration.Owners.Clear();

                var summary = Assert.Single(harness.Target.ListGroups(harness.Context));

                Assert.Equal(StagingResourceValues.StatusBlocked, summary.Status);
                Assert.False(summary.CanPromote);
                Assert.Equal(1, summary.PackageCount);
            }

            [Fact]
            public void GroupSummaryReportsValidationFailedFromReferenceParent()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.AddSymbolsOnlyEntry("PackageA", "1.0.0", parentStatus: PackageStatus.FailedValidation, group: group);

                var summary = Assert.Single(harness.Target.ListGroups(harness.Context));

                Assert.Equal(StagingResourceValues.StatusValidationFailed, summary.Status);
                Assert.Equal(1, summary.PackageCount);
                Assert.Equal(1, summary.ArtifactCount);
            }
        }

        public class TheMembershipMethods
        {
            [Fact]
            public async Task AddMovesPackageAndAdoptsGroupDeadline()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");

                var resource = await harness.Target.AddPackageToGroupAsync(harness.Context, "net10", "PackageA", "1.0.0");

                Assert.Equal(group.Key, entry.StagingGroupKey);
                Assert.Equal("net10", resource.Group.Id);
                Assert.Equal(group.ExpirationDate, resource.Expires);
                harness.Entities.VerifyCommitChanges();
            }

            [Fact]
            public async Task AddIsIdempotentWhenAlreadyInTargetGroup()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.AddStagedEntry("PackageA", "1.0.0", group: group);

                var resource = await harness.Target.AddPackageToGroupAsync(harness.Context, "net10", "PackageA", "1.0.0");

                Assert.Equal("net10", resource.Group.Id);
                harness.Entities.VerifyNoCommitChanges();
            }

            [Fact]
            public async Task AddReturnsNotFoundForMissingPackage()
            {
                var harness = new Harness();
                harness.AddGroup("net10");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.AddPackageToGroupAsync(harness.Context, "net10", "Missing", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public async Task AddRejectsWhenDestinationGroupPromoting()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                harness.AddStagedEntry("PackageA", "1.0.0");
                harness.Entities.StagingPromotionHistories.Add(new StagingPromotionHistory { GroupKey = group.Key, Status = StagingPromotionHistoryStatus.InProgress });

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.AddPackageToGroupAsync(harness.Context, "net10", "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.GroupPromoting, exception.Code);
            }

            [Fact]
            public async Task AddRejectsWhenPackagePromoting()
            {
                var harness = new Harness();
                harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0");
                entry.PackageArtifact.Status = StagingArtifactStatus.Promoting;

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.AddPackageToGroupAsync(harness.Context, "net10", "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackagePromoting, exception.Code);
                Assert.Null(entry.StagingGroupKey);
            }

            [Fact]
            public async Task RemoveCopiesGroupDeadlineOntoUngroupedPackage()
            {
                var harness = new Harness();
                var group = harness.AddGroup("net10");
                var entry = harness.AddStagedEntry("PackageA", "1.0.0", group: group);

                var resource = await harness.Target.RemovePackageFromGroupAsync(harness.Context, "net10", "PackageA", "1.0.0");

                Assert.Null(entry.StagingGroupKey);
                Assert.Equal(group.ExpirationDate, entry.ExpirationDate);
                Assert.Null(resource.Group);
            }

            [Fact]
            public async Task RemoveReturnsNotFoundWhenPackageNotInGroup()
            {
                var harness = new Harness();
                harness.AddGroup("net10");
                harness.AddStagedEntry("PackageA", "1.0.0");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.RemovePackageFromGroupAsync(harness.Context, "net10", "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }
        }

        public class TheAuthorizationBehavior
        {
            [Fact]
            public void GetHidesPackageOutsideCredentialSubjects()
            {
                var harness = new Harness();
                harness.AddStagedEntry("Hidden.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetPackage(context, "Hidden.One", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public void GetReturnsPackageWithinCredentialSubjects()
            {
                var harness = new Harness();
                harness.AddStagedEntry("Visible.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var entry = harness.Target.GetPackage(context, "Visible.One", "1.0.0");

                Assert.Equal("Visible.One", entry.Id);
            }

            [Fact]
            public async Task DownloadHidesPackageOutsideCredentialSubjects()
            {
                var harness = new Harness();
                harness.AddStagedEntry("Hidden.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DownloadPackageAsync(context, "Hidden.One", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public async Task DeleteHidesPackageOutsideCredentialSubjects()
            {
                var harness = new Harness();
                harness.AddStagedEntry("Hidden.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeletePackageAsync(context, "Hidden.One", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
                Assert.Single(harness.Entities.StagingEntries);
            }

            [Fact]
            public void ListPackagesOnlyIncludesPermittedSubjects()
            {
                var harness = new Harness();
                harness.AddStagedEntry("Visible.One", "1.0.0", key: 1);
                harness.AddStagedEntry("Hidden.One", "1.0.0", key: 2);
                var context = harness.ContextForSubjects("Visible.*");

                var list = harness.Target.ListPackages(context, groupId: null, ungrouped: false, take: 100, continuationToken: null);

                Assert.Equal("Visible.One", Assert.Single(list.Packages).Id);
            }

            [Fact]
            public void ListGroupsHidesGroupWithNoVisibleMembers()
            {
                var harness = new Harness();
                var hidden = harness.AddGroup("hidden-only");
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: hidden);
                var visible = harness.AddGroup("has-visible");
                harness.AddStagedEntry("Visible.One", "1.0.0", group: visible);
                var context = harness.ContextForSubjects("Visible.*");

                var groups = harness.Target.ListGroups(context);

                Assert.Equal("has-visible", Assert.Single(groups).Id);
            }

            [Fact]
            public void ListGroupsIncludesEmptyGroupsForOwnerScopedCredential()
            {
                var harness = new Harness();
                harness.AddGroup("empty-group");
                var context = harness.ContextForSubjects("Visible.*");

                var summary = Assert.Single(harness.Target.ListGroups(context));

                Assert.Equal("empty-group", summary.Id);
                Assert.Equal(0, summary.PackageCount);
            }

            [Fact]
            public void ListGroupsComputesSummaryOverVisibleMembersOnly()
            {
                var harness = new Harness();
                var group = harness.AddGroup("mixed");
                harness.AddStagedEntry("Visible.One", "1.0.0", group: group);
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group, packageStatus: StagingArtifactStatus.ValidationFailed);
                var context = harness.ContextForSubjects("Visible.*");

                var summary = Assert.Single(harness.Target.ListGroups(context));

                Assert.Equal(1, summary.PackageCount);
                Assert.Equal(1, summary.ArtifactCount);
                Assert.Equal(StagingResourceValues.StatusReady, summary.Status);
            }

            [Fact]
            public void GetGroupReturnsNotFoundWhenNoVisibleMembers()
            {
                var harness = new Harness();
                var group = harness.AddGroup("hidden-only");
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetGroup(context, "hidden-only", take: 100, continuationToken: null));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }

            [Fact]
            public void GetGroupExcludesUnpermittedPackagesFromDetail()
            {
                var harness = new Harness();
                var group = harness.AddGroup("mixed");
                harness.AddStagedEntry("Visible.One", "1.0.0", group: group);
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var detail = harness.Target.GetGroup(context, "mixed", take: 100, continuationToken: null);

                Assert.Equal(1, detail.PackageCount);
                Assert.Equal("Visible.One", Assert.Single(detail.Packages).Id);
            }

            [Fact]
            public async Task RenameRejectsGroupWithMemberOutsideCredentialSubjects()
            {
                var harness = new Harness();
                var group = harness.AddGroup("mixed");
                harness.AddStagedEntry("Visible.One", "1.0.0", group: group);
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.RenameGroupAsync(context, "mixed", new StagingRenameGroupRequest { Name = "Renamed" }));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
                Assert.Equal("mixed", harness.Entities.StagingGroups.Single().Name);
            }

            [Fact]
            public async Task DeleteRejectsGroupWithMemberOutsideCredentialSubjects()
            {
                var harness = new Harness();
                var group = harness.AddGroup("mixed");
                harness.AddStagedEntry("Visible.One", "1.0.0", group: group);
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeleteGroupAsync(context, "mixed"));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
                Assert.Single(harness.Entities.StagingGroups);
                Assert.Equal(2, harness.Entities.StagingEntries.Count());
            }

            [Fact]
            public async Task CreateEmptyGroupAllowedWithNarrowSubjectCredential()
            {
                var harness = new Harness();
                var context = harness.ContextForSubjects("Visible.*");

                var result = await harness.Target.CreateGroupAsync(context, new StagingCreateGroupRequest { Id = "net10-preview" });

                Assert.True(result.Created);
                Assert.Single(harness.Entities.StagingGroups);
            }

            [Fact]
            public async Task AddRejectsPackageOutsideCredentialSubjects()
            {
                var harness = new Harness();
                harness.AddGroup("net10");
                harness.AddStagedEntry("Hidden.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.AddPackageToGroupAsync(context, "net10", "Hidden.One", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public async Task AddRejectsHiddenDestinationGroup()
            {
                var harness = new Harness();
                var destination = harness.AddGroup("hidden-only");
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: destination);
                harness.AddStagedEntry("Visible.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.AddPackageToGroupAsync(context, "hidden-only", "Visible.One", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
                Assert.Null(harness.Entities.StagingEntries.Single(x => x.Package.PackageRegistration.Id == "Visible.One").StagingGroupKey);
            }

            [Fact]
            public async Task AddAllowsEmptyDestinationGroup()
            {
                var harness = new Harness();
                var destination = harness.AddGroup("empty-target");
                var entry = harness.AddStagedEntry("Visible.One", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var resource = await harness.Target.AddPackageToGroupAsync(context, "empty-target", "Visible.One", "1.0.0");

                Assert.Equal("empty-target", resource.Group.Id);
                Assert.Equal(destination.Key, entry.StagingGroupKey);
            }

            [Fact]
            public async Task AddAllowsPartiallyVisibleDestinationGroup()
            {
                var harness = new Harness();
                var destination = harness.AddGroup("mixed-target");
                harness.AddStagedEntry("Visible.Existing", "1.0.0", group: destination);
                harness.AddStagedEntry("Hidden.Existing", "1.0.0", group: destination);
                var entry = harness.AddStagedEntry("Visible.New", "1.0.0");
                var context = harness.ContextForSubjects("Visible.*");

                var resource = await harness.Target.AddPackageToGroupAsync(context, "mixed-target", "Visible.New", "1.0.0");

                Assert.Equal("mixed-target", resource.Group.Id);
                Assert.Equal(destination.Key, entry.StagingGroupKey);
            }

            [Fact]
            public void RemovedMemberCannotReadExactPackage()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0");
                var removedMember = new User { Key = 500, Username = "removed" };
                var context = harness.ContextForActor(removedMember, harness.Owner);

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.GetPackage(context, "PackageA", "1.0.0"));

                Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
            }

            [Fact]
            public void RemovedMemberCannotListGroups()
            {
                var harness = new Harness();
                harness.AddGroup("net10");
                var removedMember = new User { Key = 500, Username = "removed" };
                var context = harness.ContextForActor(removedMember, harness.Owner);

                var exception = Assert.Throws<StagingApiException>(() => harness.Target.ListGroups(context));

                Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }

            [Fact]
            public async Task RemovedMemberCannotDeletePackage()
            {
                var harness = new Harness();
                harness.AddStagedEntry("PackageA", "1.0.0");
                var removedMember = new User { Key = 500, Username = "removed" };
                var context = harness.ContextForActor(removedMember, harness.Owner);

                var exception = await Assert.ThrowsAsync<StagingApiException>(() => harness.Target.DeletePackageAsync(context, "PackageA", "1.0.0"));

                Assert.Equal(StagingApiErrorCodes.StagedPackageNotFound, exception.Code);
                Assert.Single(harness.Entities.StagingEntries);
            }

            [Fact]
            public void ListPackagesByHiddenGroupReturnsNotFound()
            {
                var harness = new Harness();
                var group = harness.AddGroup("hidden-only");
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var exception = Assert.Throws<StagingApiException>(() =>
                    harness.Target.ListPackages(context, groupId: "hidden-only", ungrouped: false, take: 100, continuationToken: null));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }

            [Fact]
            public async Task CreateGroupForHiddenExistingGroupReturnsNotFound()
            {
                var harness = new Harness();
                var group = harness.AddGroup("hidden-only");
                harness.AddStagedEntry("Hidden.One", "1.0.0", group: group);
                var context = harness.ContextForSubjects("Visible.*");

                var exception = await Assert.ThrowsAsync<StagingApiException>(() =>
                    harness.Target.CreateGroupAsync(context, new StagingCreateGroupRequest { Id = "hidden-only" }));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
            }
        }

        private sealed class Harness
        {
            private int _nextKey = 1000;            public DateTime Now { get; } = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

            public Harness()
            {
                Owner = new User { Key = 1, Username = "owner", EmailAddress = "owner@example.com" };
                Context = ContextForOwner(Owner);
                Entities = new FakeEntitiesContext();
                Transaction = new Mock<IDbContextTransaction>();
                Database = new Mock<IDatabase>();
                Database.Setup(x => x.BeginTransaction()).Returns(Transaction.Object);
                Database.Setup(x => x.ExecuteSqlCommandAsync(It.IsAny<string>())).ReturnsAsync(0);
                Entities.SetupDatabase(Database.Object);
                BlobService = new Mock<IStagingBlobService>();
                var dateTimeProvider = new Mock<IDateTimeProvider>();
                dateTimeProvider.SetupGet(x => x.UtcNow).Returns(Now);
                var appConfiguration = new Mock<IAppConfiguration>();
                appConfiguration.SetupGet(x => x.SiteRoot).Returns("https://nuget.test/");

                Target = new StagingService(
                    Entities,
                    Mock.Of<IPackageService>(),
                    Mock.Of<IPackageUploadService>(),
                    Mock.Of<ISymbolPackageService>(),
                    Mock.Of<IReservedNamespaceService>(),
                    Mock.Of<IApiScopeEvaluator>(),
                    BlobService.Object,
                    Mock.Of<IStagingValidationMessageEmitter>(),
                    dateTimeProvider.Object,
                    Mock.Of<IMessageService>(),
                    Mock.Of<IMessageServiceConfiguration>(),
                    appConfiguration.Object,
                    new TestStagingTokenProtector());
            }

            public User Owner { get; }
            public StagingAuthorizationContext Context { get; }
            public FakeEntitiesContext Entities { get; }
            public Mock<IDbContextTransaction> Transaction { get; }
            public Mock<IDatabase> Database { get; }
            public Mock<IStagingBlobService> BlobService { get; }
            public StagingService Target { get; }

            public StagingAuthorizationContext ContextForOwner(User owner)
            {
                return BuildContext(owner, owner, NuGetPackagePattern.AllInclusivePattern);
            }

            public StagingAuthorizationContext ContextForSubjects(params string[] subjects)
            {
                return BuildContext(Owner, Owner, subjects);
            }

            public StagingAuthorizationContext ContextForActor(User currentUser, User owner, params string[] subjects)
            {
                var effective = subjects.Length == 0 ? new[] { NuGetPackagePattern.AllInclusivePattern } : subjects;
                return BuildContext(currentUser, owner, effective);
            }

            private StagingAuthorizationContext BuildContext(User currentUser, User owner, params string[] subjects)
            {
                var scopes = subjects
                    .Select(subject => new Scope(owner, subject, NuGetScopes.PackageStage) { OwnerKey = owner.Key })
                    .ToArray();
                var credential = new Credential(CredentialTypes.ApiKey.V4, "api-key") { Scopes = scopes };
                return new StagingAuthorizationContext(currentUser, owner, credential);
            }

            public StagingGroup AddGroup(string id)
            {
                var group = new StagingGroup
                {
                    Key = _nextKey++,
                    Owner = Owner,
                    OwnerKey = Owner.Key,
                    Id = id,
                    Name = id,
                    CreatedDate = Now.AddDays(-2),
                    ExpirationDate = Now.AddDays(20),
                };
                Entities.StagingGroups.Add(group);
                return group;
            }

            public StagingEntry AddStagedEntry(
                string id,
                string version,
                int? key = null,
                StagingGroup group = null,
                StagingArtifactStatus packageStatus = StagingArtifactStatus.Ready,
                User owner = null)
            {
                var entryOwner = owner ?? Owner;
                var package = CreatePackage(id, version, entryOwner, PackageStatus.Staged);
                var entryKey = key ?? _nextKey++;
                var entry = new StagingEntry
                {
                    Key = entryKey,
                    Package = package,
                    PackageKey = package.Key,
                    Owner = entryOwner,
                    OwnerKey = entryOwner.Key,
                    CreatedDate = Now.AddDays(-1),
                    ExpirationDate = Now.AddDays(29),
                    StagingGroup = group,
                    StagingGroupKey = group?.Key,
                };
                entry.PackageArtifact = new StagedPackageArtifact
                {
                    StagingEntry = entry,
                    StagingEntryKey = entryKey,
                    BlobPath = "v1/2026/08/10/" + Guid.NewGuid().ToString("N") + ".nupkg",
                    BlobETag = "package-etag-" + entryKey,
                    ContentHash = "package-hash-" + entryKey,
                    Status = packageStatus,
                    ValidationTrackingId = Guid.NewGuid(),
                    UploadedDate = Now.AddDays(-1),
                };
                Entities.StagingEntries.Add(entry);
                Entities.StagedPackageArtifacts.Add(entry.PackageArtifact);
                group?.Entries.Add(entry);
                return entry;
            }

            public StagingEntry AddSymbolsOnlyEntry(string id, string version, PackageStatus parentStatus, int? key = null, StagingGroup group = null)
            {
                var package = CreatePackage(id, version, Owner, parentStatus);
                var entryKey = key ?? _nextKey++;
                var entry = new StagingEntry
                {
                    Key = entryKey,
                    Package = package,
                    PackageKey = package.Key,
                    Owner = Owner,
                    OwnerKey = Owner.Key,
                    CreatedDate = Now.AddDays(-1),
                    ExpirationDate = Now.AddDays(29),
                    StagingGroup = group,
                    StagingGroupKey = group?.Key,
                };
                Entities.StagingEntries.Add(entry);
                group?.Entries.Add(entry);
                AddSymbolArtifact(entry, StagingArtifactStatus.Ready);
                return entry;
            }

            public StagedSymbolArtifact AddSymbolArtifact(StagingEntry entry, StagingArtifactStatus status)
            {
                var symbolPackage = new SymbolPackage
                {
                    Key = _nextKey++,
                    Package = entry.Package,
                    PackageKey = entry.Package.Key,
                    Hash = "symbol-hash-" + entry.Key,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                    StatusKey = PackageStatus.Staged,
                    FileSize = 200,
                };
                entry.Package.SymbolPackages.Add(symbolPackage);
                var artifact = new StagedSymbolArtifact
                {
                    StagingEntry = entry,
                    StagingEntryKey = entry.Key,
                    SymbolPackage = symbolPackage,
                    SymbolPackageKey = symbolPackage.Key,
                    BlobPath = "v1/2026/08/10/" + Guid.NewGuid().ToString("N") + ".snupkg",
                    BlobETag = "symbol-etag-" + entry.Key,
                    ContentHash = "symbol-hash-" + entry.Key,
                    ParentContentHash = entry.PackageArtifact?.ContentHash ?? entry.Package.Hash,
                    Status = status,
                    ValidationTrackingId = Guid.NewGuid(),
                    UploadedDate = Now.AddDays(-1),
                };
                entry.SymbolArtifact = artifact;
                Entities.SymbolPackages.Add(symbolPackage);
                Entities.StagedSymbolArtifacts.Add(artifact);
                return artifact;
            }

            public void AttachPromotion(StagedPackageArtifact artifact, Guid promotionId, DateTime startedDate)
            {
                var history = new StagingPromotionHistory
                {
                    Key = _nextKey++,
                    Id = promotionId,
                    OwnerKey = Owner.Key,
                    Status = StagingPromotionHistoryStatus.InProgress,
                    RequestedDate = startedDate,
                };
                var artifactHistory = new StagingPromotionArtifactHistory
                {
                    Key = _nextKey++,
                    StagingPromotionHistory = history,
                    StagingPromotionHistoryKey = history.Key,
                };
                Entities.StagingPromotionHistories.Add(history);
                Entities.StagingPromotionArtifactHistories.Add(artifactHistory);
                artifact.PromotionArtifactHistory = artifactHistory;
                artifact.PromotionArtifactHistoryKey = artifactHistory.Key;
                artifact.PromotionStartedDate = startedDate;
            }

            private Package CreatePackage(string id, string version, User owner, PackageStatus status)
            {
                var registration = Entities.PackageRegistrations.SingleOrDefault(x => x.Id == id);
                if (registration == null)
                {
                    registration = new PackageRegistration { Key = _nextKey++, Id = id };
                    registration.Owners.Add(owner);
                    Entities.PackageRegistrations.Add(registration);
                }

                var package = new Package
                {
                    Key = _nextKey++,
                    Id = id,
                    Version = version,
                    NormalizedVersion = NuGetVersion.Parse(version).ToNormalizedString(),
                    PackageRegistration = registration,
                    PackageStatusKey = status,
                    PackageFileSize = 100,
                    Hash = "parent-hash-" + id,
                    User = owner,
                };
                registration.Packages.Add(package);
                Entities.Packages.Add(package);
                return package;
            }
        }
    }

    internal sealed class TestStagingTokenProtector : IStagingTokenProtector
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("staging-facts-token-protector-key-0123456789");

        public string Protect(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            using (var hmac = new System.Security.Cryptography.HMACSHA256(Key))
            {
                var signature = hmac.ComputeHash(payload);
                var combined = new byte[signature.Length + payload.Length];
                Buffer.BlockCopy(signature, 0, combined, 0, signature.Length);
                Buffer.BlockCopy(payload, 0, combined, signature.Length, payload.Length);
                return Convert.ToBase64String(combined);
            }
        }

        public byte[] Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return null;
            }

            byte[] combined;
            try
            {
                combined = Convert.FromBase64String(protectedValue);
            }
            catch (FormatException)
            {
                return null;
            }

            if (combined.Length < 32)
            {
                return null;
            }

            var signature = new byte[32];
            var payload = new byte[combined.Length - 32];
            Buffer.BlockCopy(combined, 0, signature, 0, 32);
            Buffer.BlockCopy(combined, 32, payload, 0, payload.Length);

            using (var hmac = new System.Security.Cryptography.HMACSHA256(Key))
            {
                var expected = hmac.ComputeHash(payload);
                if (!FixedTimeEquals(expected, signature))
                {
                    return null;
                }
            }

            return payload;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }
}
