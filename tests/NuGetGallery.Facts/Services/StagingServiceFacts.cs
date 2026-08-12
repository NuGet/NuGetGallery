// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Packaging;
using Xunit;

namespace NuGetGallery
{
    public class StagingServiceFacts
    {
        [Fact]
        public void UsesTheDefaultArtifactLimit()
        {
            Assert.Equal(350, StagingService.DefaultArtifactLimit);
        }

        [Fact]
        public async Task RejectsRequestWithoutArtifacts()
        {
            var facts = new Facts();

            var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest()));

            Assert.Equal(StagingApiErrorCodes.InvalidMultipart, exception.Code);
        }

        [Fact]
        public async Task RejectsMismatchedPairedIdentityBeforeWritingBlobs()
        {
            var facts = new Facts();
            using (var package = CreatePackage("PackageA", "1.0.0"))
            using (var symbols = CreatePackage("PackageB", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest
                {
                    Package = package,
                    Symbols = symbols,
                }));

                Assert.Equal(StagingApiErrorCodes.ArtifactIdentityMismatch, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task CreatesPackageAndDispatchesAfterKeyFlush()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupPackageGeneration("PackageA", "1.0.0", key: 42);
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest { Package = package });

                Assert.True(result.Created);
                Assert.Equal(StagingResourceValues.OperationCreated, result.Package.Package.Operation);
                Assert.Equal(StagingResourceValues.StatusValidating, result.Package.Package.Status);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.Is<Package>(p => p.Key == 42 && p.PackageStatusKey == PackageStatus.Staged), It.IsAny<Guid>()), Times.Once);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
                Assert.Empty(facts.Entities.StagingBlobCleanups);
                facts.MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), false, false), Times.Once);
            }
        }

        [Fact]
        public async Task CreatesPairedArtifactsAndDispatchesBothBeforeCommit()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupPackageGeneration("PackageA", "1.0.0", key: 42);
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "nupkg-etag", "nupkg-hash");
            facts.SetupBlob(StagingBlobType.Snupkg, "new.snupkg", "snupkg-etag", "snupkg-hash");
            facts.SymbolPackageService
                .Setup(x => x.CreateSymbolPackage(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>()))
                .Returns((Package parent, PackageStreamMetadata metadata) =>
                {
                    var symbol = new SymbolPackage
                    {
                        Key = 43,
                        Package = parent,
                        PackageKey = parent.Key,
                        Hash = metadata.Hash,
                        HashAlgorithm = metadata.HashAlgorithm,
                        StatusKey = PackageStatus.Staged,
                    };
                    parent.SymbolPackages.Add(symbol);
                    facts.Entities.SymbolPackages.Add(symbol);
                    return symbol;
                });

            using (var package = CreatePackage("PackageA", "1.0.0"))
            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest
                    {
                        Package = package,
                        Symbols = symbols,
                    });

                Assert.True(result.Created);
                Assert.Equal(StagingResourceValues.OperationCreated, result.Package.Package.Operation);
                Assert.Equal(StagingResourceValues.OperationCreated, result.Package.Symbols.Operation);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.Is<Package>(p => p.Key == 42), It.IsAny<Guid>()), Times.Once);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.Is<SymbolPackage>(p => p.Key == 43), It.IsAny<Guid>()), Times.Once);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
                facts.MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), false, false), Times.Once);
            }
        }

        [Fact]
        public async Task IdenticalPackageIsNoOpWithoutBlobWriteOrDispatch()
        {
            var facts = new Facts();
            using (var packageStream = CreatePackage("PackageA", "1.0.0"))
            {
                var hash = GetHash(packageStream);
                facts.AddExistingStagedPackage(packageHash: hash, symbolHash: null);

                var result = await facts.UploadAsync(new StagingUploadRequest { Package = packageStream });

                Assert.False(result.Created);
                Assert.Equal(StagingResourceValues.OperationUnchanged, result.Package.Package.Operation);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.IsAny<Package>(), It.IsAny<Guid>()), Times.Never);
                facts.Transaction.Verify(x => x.Commit(), Times.Never);
                facts.Entities.VerifyNoCommitChanges();
                facts.MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Fact]
        public async Task ChangedPackageRevalidatesIdenticalRetainedSymbols()
        {
            var facts = new Facts();
            using (var packageStream = CreatePackage("PackageA", "1.0.0", description: "replacement"))
            using (var symbolsStream = CreatePackage("PackageA", "1.0.0"))
            {
                var symbolHash = GetHash(symbolsStream);
                var state = facts.AddExistingStagedPackage(packageHash: "old-package-hash", symbolHash: symbolHash);
                state.Package.Listed = false;
                facts.SetupBlob(StagingBlobType.Nupkg, "replacement.nupkg", "replacement-etag", "replacement-hash");
                facts.PackageService
                    .Setup(x => x.EnrichPackageFromNuGetPackage(state.Package, It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<PackageStreamMetadata>(), facts.CurrentUser))
                    .Returns((
                        Package package,
                        PackageArchiveReader _,
                        PackageMetadata __,
                        PackageStreamMetadata ___,
                        User ____) =>
                    {
                        package.Listed = true;
                        return package;
                    });

                var oldSymbolPackage = state.Entry.SymbolArtifact.SymbolPackage;
                var result = await facts.UploadAsync(new StagingUploadRequest
                    {
                        Package = packageStream,
                        Symbols = symbolsStream,
                    });

                Assert.Equal(StagingResourceValues.OperationReplaced, result.Package.Package.Operation);
                Assert.Equal(StagingResourceValues.OperationUnchanged, result.Package.Symbols.Operation);
                Assert.False(state.Package.Listed);
                Assert.Same(oldSymbolPackage, state.Entry.SymbolArtifact.SymbolPackage);
                Assert.Equal("old-symbol.nupkg", state.Entry.SymbolArtifact.BlobPath);
                Assert.Equal(state.Entry.PackageArtifact.ContentHash, state.Entry.SymbolArtifact.ParentContentHash);
                facts.SymbolPackageService.Verify(x => x.CreateSymbolPackage(It.IsAny<Package>(), It.IsAny<PackageStreamMetadata>()), Times.Never);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(oldSymbolPackage, It.IsAny<Guid>()), Times.Once);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
                var cleanup = Assert.Single(facts.Entities.StagingBlobCleanups);
                Assert.Equal("old-package.nupkg", cleanup.BlobPath);
            }
        }

        [Fact]
        public async Task ReplacesSymbolsWithoutRevalidatingPackage()
        {
            var facts = new Facts();
            var state = facts.AddExistingStagedPackage(packageHash: "package-hash", symbolHash: "old-symbol-hash");
            facts.SetupBlob(StagingBlobType.Snupkg, "replacement.snupkg", "replacement-etag", "replacement-hash");
            facts.SetupSymbolGeneration(state.Package, key: 44);
            var oldSymbolPackage = state.Entry.SymbolArtifact.SymbolPackage;

            using (var symbols = CreatePackage("PackageA", "1.0.0", "replacement"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest { Symbols = symbols });

                Assert.False(result.Created);
                Assert.Equal(StagingResourceValues.OperationReplaced, result.Package.Symbols.Operation);
                Assert.Equal("replacement.snupkg", state.Entry.SymbolArtifact.BlobPath);
                Assert.DoesNotContain(oldSymbolPackage, facts.Entities.SymbolPackages);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.Is<SymbolPackage>(p => p.Key == 44), It.IsAny<Guid>()), Times.Once);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.IsAny<Package>(), It.IsAny<Guid>()), Times.Never);
                var cleanup = Assert.Single(facts.Entities.StagingBlobCleanups);
                Assert.Equal("old-symbol.nupkg", cleanup.BlobPath);
            }
        }

        [Fact]
        public async Task RejectsReplacementWhenTheEntryChangesDuringBlobUpload()
        {
            var facts = new Facts();
            var state = facts.AddExistingStagedPackage(packageHash: "package-hash", symbolHash: "old-symbol-hash");
            facts.BlobService
                .Setup(x => x.CreateAsync(It.IsAny<Stream>(), StagingBlobType.Snupkg))
                .ReturnsAsync(() =>
                {
                    state.Entry.SymbolArtifact.ContentHash = "concurrent-symbol-hash";
                    return new StagingBlobReference("replacement.snupkg", "replacement-etag", "replacement-hash", 100, StagingBlobType.Snupkg);
                });

            using (var symbols = CreatePackage("PackageA", "1.0.0", "replacement"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Symbols = symbols }));

                Assert.Equal(StagingApiErrorCodes.PackageVersionConflict, exception.Code);
                facts.Transaction.Verify(x => x.Commit(), Times.Never);
            }
        }

        [Fact]
        public async Task ChangesListedIntentWithoutRevalidatingOrRefreshingExpiration()
        {
            var facts = new Facts();
            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var state = facts.AddExistingStagedPackage(packageHash: GetHash(package), symbolHash: null);
                state.Package.Listed = true;
                var expiration = state.Entry.ExpirationDate;

                var result = await facts.UploadAsync(new StagingUploadRequest
                    {
                        Package = package,
                        Listed = false,
                    });

                Assert.False(result.Created);
                Assert.False(state.Package.Listed);
                Assert.Equal(expiration, state.Entry.ExpirationDate);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.IsAny<Package>(), It.IsAny<Guid>()), Times.Never);
                facts.MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Fact]
        public async Task CreatesPackageInRequestedGroup()
        {
            var facts = new Facts();
            var group = facts.AddGroup(key: 20, id: "release");
            facts.SetupExistingRegistration("PackageA");
            facts.SetupPackageGeneration("PackageA", "1.0.0", key: 42);
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest
                    {
                        Package = package,
                        GroupId = group.Id,
                    });

                Assert.True(result.Created);
                Assert.Equal(group.Id, result.Package.Group.Id);
                Assert.Same(group, facts.Entities.StagingEntries.Single().StagingGroup);
            }
        }

        [Fact]
        public async Task MovesUnchangedEntryToRequestedGroupWithoutRevalidating()
        {
            var facts = new Facts();
            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var state = facts.AddExistingStagedPackage(packageHash: GetHash(package), symbolHash: null);
                var oldGroup = facts.AddGroup(key: 20, id: "old");
                var newGroup = facts.AddGroup(key: 21, id: "new");
                state.Entry.StagingGroup = oldGroup;
                state.Entry.StagingGroupKey = oldGroup.Key;

                var result = await facts.UploadAsync(new StagingUploadRequest
                    {
                        Package = package,
                        GroupId = newGroup.Id,
                    });

                Assert.False(result.Created);
                Assert.Equal(newGroup.Id, result.Package.Group.Id);
                Assert.Same(newGroup, state.Entry.StagingGroup);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.IsAny<Package>(), It.IsAny<Guid>()), Times.Never);
            }
        }

        [Theory]
        [InlineData(PackageStatus.Available, "available")]
        [InlineData(PackageStatus.Validating, StagingResourceValues.StatusValidating)]
        [InlineData(PackageStatus.FailedValidation, StagingResourceValues.StatusValidationFailed)]
        public async Task CreatesSymbolsBesideEligibleParent(PackageStatus parentStatus, string expectedStatus)
        {
            var facts = new Facts();
            var parent = facts.AddParent(parentStatus, ownerOwns: true);
            facts.SetupBlob(StagingBlobType.Snupkg, "new.snupkg", "snupkg-etag", "snupkg-hash");
            facts.SymbolPackageService
                .Setup(x => x.CreateSymbolPackage(parent, It.IsAny<PackageStreamMetadata>()))
                .Returns((Package package, PackageStreamMetadata metadata) =>
                {
                    var symbol = new SymbolPackage
                    {
                        Key = 43,
                        Package = package,
                        PackageKey = package.Key,
                        Hash = metadata.Hash,
                        HashAlgorithm = metadata.HashAlgorithm,
                        StatusKey = PackageStatus.Staged,
                    };
                    package.SymbolPackages.Add(symbol);
                    return symbol;
                });

            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest { Symbols = symbols });

                Assert.True(result.Created);
                Assert.Equal(StagingResourceValues.SourceReference, result.Package.Package.Source);
                Assert.Equal(expectedStatus, result.Package.Package.Status);
                Assert.Equal(parent.Hash, facts.Entities.StagingEntries.Single().SymbolArtifact.ParentContentHash);
                facts.ValidationEmitter.Verify(x => x.StartValidationAsync(It.Is<SymbolPackage>(p => p.Key == 43), It.IsAny<Guid>()), Times.Once);
            }
        }

        [Theory]
        [InlineData(PackageStatus.Available, false)]
        [InlineData(PackageStatus.Deleted, true)]
        public async Task HidesIneligibleParent(PackageStatus parentStatus, bool ownerOwns)
        {
            var facts = new Facts();
            facts.AddParent(parentStatus, ownerOwns);

            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Symbols = symbols }));

                Assert.Equal(StagingApiErrorCodes.ParentNotFound, exception.Code);
            }
        }

        [Theory]
        [InlineData("UPPERCASE")]
        [InlineData("-leading-hyphen")]
        [InlineData("trailing-hyphen-")]
        public async Task RejectsInvalidGroupId(string groupId)
        {
            var facts = new Facts();
            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest
                {
                    Package = package,
                    GroupId = groupId,
                }));

                Assert.Equal(StagingApiErrorCodes.InvalidGroupId, exception.Code);
                Assert.Equal("groupId", exception.Target);
            }
        }

        [Fact]
        public async Task RejectsMissingGroup()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest
                {
                    Package = package,
                    GroupId = "missing",
                }));

                Assert.Equal(StagingApiErrorCodes.GroupNotFound, exception.Code);
                Assert.Equal("groupId", exception.Target);
            }
        }

        [Fact]
        public async Task RejectsListedOnSymbolOnlyUpload()
        {
            var facts = new Facts();
            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest
                {
                    Symbols = symbols,
                    Listed = false,
                }));

                Assert.Equal(StagingApiErrorCodes.InvalidMultipart, exception.Code);
                Assert.Equal("listed", exception.Target);
            }
        }

        [Fact]
        public async Task RejectsPackageOutsideCredentialSubject()
        {
            var facts = new Facts();
            facts.Credential.Scopes.Single().Subject = "Other.*";
            facts.SetupExistingRegistration("PackageA");

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.StagingScopeRequired, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task RejectsPackageForLockedRegistration()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA").IsLocked = true;

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.PackageVersionConflict, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task RejectsUnauthorizedSubjectBeforeRevealingLockedRegistration()
        {
            var facts = new Facts();
            facts.Credential.Scopes.Single().Subject = "Other.*";
            facts.SetupExistingRegistration("PackageA").IsLocked = true;

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.StagingScopeRequired, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task HidesLockedParentForSymbolOnlyUpload()
        {
            var facts = new Facts();
            var parent = facts.AddParent(PackageStatus.Available, ownerOwns: true);
            parent.PackageRegistration.IsLocked = true;

            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Symbols = symbols }));

                Assert.Equal(StagingApiErrorCodes.ParentNotFound, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task RejectsReplacementAfterPromotionClaim()
        {
            var facts = new Facts();
            var state = facts.AddExistingStagedPackage(packageHash: "old-package-hash", symbolHash: null);
            state.Entry.PackageArtifact.PromotionArtifactHistoryKey = 123;

            using (var package = CreatePackage("PackageA", "1.0.0", "replacement"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.StagedPackagePromoting, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task RejectsReplacementWhileCurrentOrRequestedGroupIsPromoting()
        {
            var facts = new Facts();
            var state = facts.AddExistingStagedPackage(packageHash: "old-package-hash", symbolHash: null);
            var group = facts.AddGroup(key: 20, id: "release");
            state.Entry.StagingGroup = group;
            state.Entry.StagingGroupKey = group.Key;
            facts.Entities.StagingPromotionHistories.Add(new StagingPromotionHistory
            {
                Key = 30,
                Owner = facts.Owner,
                OwnerKey = facts.Owner.Key,
                Group = group,
                GroupKey = group.Key,
                Status = StagingPromotionHistoryStatus.InProgress,
            });

            using (var package = CreatePackage("PackageA", "1.0.0", "replacement"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.GroupPromoting, exception.Code);
                facts.BlobService.Verify(x => x.CreateAsync(It.IsAny<Stream>(), It.IsAny<StagingBlobType>()), Times.Never);
            }
        }

        [Fact]
        public async Task RejectsCreateBeyondOwnerArtifactQuota()
        {
            var facts = new Facts();
            facts.Owner.StagingArtifactLimit = 0;
            facts.SetupExistingRegistration("PackageA");

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.ArtifactQuotaExceeded, exception.Code);
            }
        }

        [Fact]
        public async Task MessagingFailureRollsBackLiveTransaction()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupPackageGeneration("PackageA", "1.0.0", key: 42);
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");
            facts.ValidationEmitter
                .Setup(x => x.StartValidationAsync(It.IsAny<Package>(), It.IsAny<Guid>()))
                .ThrowsAsync(new ServiceBusException("Unavailable", ServiceBusFailureReason.ServiceBusy));

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                Assert.Equal(StagingApiErrorCodes.StagingUnavailable, exception.Code);
                TransactionWasNotCommitted(facts);
                facts.BlobService.Verify(x => x.DeleteAsync("new.nupkg", "new-etag"), Times.Once);
                facts.MessageService.Verify(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Fact]
        public async Task PairedBlobFailureDeletesTheWrittenBlob()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");
            facts.BlobService
                .Setup(x => x.CreateAsync(It.IsAny<Stream>(), StagingBlobType.Snupkg))
                .ThrowsAsync(new CloudBlobStorageException("Unavailable"));

            using (var package = CreatePackage("PackageA", "1.0.0"))
            using (var symbols = CreatePackage("PackageA", "1.0.0"))
            {
                var exception = await Assert.ThrowsAsync<StagingApiException>(() => facts.UploadAsync(new StagingUploadRequest
                {
                    Package = package,
                    Symbols = symbols,
                }));

                Assert.Equal(StagingApiErrorCodes.StagingUnavailable, exception.Code);
                facts.BlobService.Verify(x => x.DeleteAsync("new.nupkg", "new-etag"), Times.Once);
                Assert.Empty(facts.Entities.StagingBlobCleanups);
                facts.Database.Verify(x => x.BeginTransaction(), Times.Never);
            }
        }

        [Fact]
        public async Task DatabaseFailureDeletesNewBlob()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");
            facts.Database
                .Setup(x => x.ExecuteSqlCommandAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE"))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => facts.UploadAsync(new StagingUploadRequest { Package = package }));

                facts.BlobService.Verify(x => x.DeleteAsync("new.nupkg", "new-etag"), Times.Once);
                Assert.Empty(facts.Entities.StagingBlobCleanups);
                TransactionWasNotCommitted(facts);
            }
        }

        [Fact]
        public async Task DigestEnqueueFailureDoesNotFailCommittedUpload()
        {
            var facts = new Facts();
            facts.SetupExistingRegistration("PackageA");
            facts.SetupPackageGeneration("PackageA", "1.0.0", key: 42);
            facts.SetupBlob(StagingBlobType.Nupkg, "new.nupkg", "new-etag", "new-hash");
            facts.MessageService
                .Setup(x => x.SendMessageAsync(It.IsAny<IEmailBuilder>(), false, false))
                .ThrowsAsync(new ServiceBusException("Unavailable", ServiceBusFailureReason.ServiceBusy));

            using (var package = CreatePackage("PackageA", "1.0.0"))
            {
                var result = await facts.UploadAsync(new StagingUploadRequest { Package = package });

                Assert.True(result.Created);
                facts.Transaction.Verify(x => x.Commit(), Times.Once);
            }
        }

        private static void TransactionWasNotCommitted(Facts facts)
        {
            facts.Transaction.Verify(x => x.Commit(), Times.Never);
        }

        private static MemoryStream CreatePackage(string id, string version, string description = "Description")
        {
            var builder = new PackageBuilder
            {
                Id = id,
                Version = NuGetVersion.Parse(version),
                Description = description,
            };
            builder.Authors.Add("Author");
            builder.DependencyGroups.Add(new PackageDependencyGroup(
                NuGetFramework.AnyFramework,
                new[]
                {
                    new NuGet.Packaging.Core.PackageDependency("Dependency", VersionRange.Parse("[1.0.0,)")),
                }));

            var stream = new MemoryStream();
            builder.Save(stream);
            stream.Position = 0;
            return stream;
        }

        private static string GetHash(Stream stream)
        {
            var hash = CryptographyService.GenerateHash(stream.AsSeekableStream(), CoreConstants.Sha512HashAlgorithmId);
            stream.Position = 0;
            return hash;
        }

        private sealed class Facts
        {
            private readonly DateTime _now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

            public Facts()
            {
                CurrentUser = new User
                {
                    Key = 1,
                    Username = "member",
                    EmailAddress = "member@example.com",
                };
                Owner = CurrentUser;
                Credential = new Credential(CredentialTypes.ApiKey.V4, "api-key")
                {
                    Scopes = new[]
                    {
                        new Scope(Owner, NuGetPackagePattern.AllInclusivePattern, NuGetScopes.PackageStage)
                        {
                            OwnerKey = Owner.Key,
                        },
                    },
                };
                Entities = new FakeEntitiesContext();
                Transaction = new Mock<IDbContextTransaction>();
                Database = new Mock<IDatabase>();
                Database.Setup(x => x.BeginTransaction()).Returns(Transaction.Object);
                Database
                    .Setup(x => x.ExecuteSqlCommandAsync("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE"))
                    .ReturnsAsync(0);
                Entities.SetupDatabase(Database.Object);

                PackageService = new Mock<IPackageService>();
                PackageService
                    .Setup(x => x.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);
                PackageService
                    .Setup(x => x.UpdateIsLatestAsync(It.IsAny<PackageRegistration>(), false))
                    .Returns(Task.CompletedTask);

                PackageUploadService = new Mock<IPackageUploadService>();
                PackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageMetadata>(),
                        It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.Accepted());
                PackageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<bool>()))
                    .ReturnsAsync(PackageValidationResult.Accepted());

                SymbolPackageService = new Mock<ISymbolPackageService>();
                SymbolPackageService
                    .Setup(x => x.EnsureValidAsync(It.IsAny<PackageArchiveReader>()))
                    .Returns(Task.CompletedTask);

                ReservedNamespaceService = new Mock<IReservedNamespaceService>();
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByKey(Owner.Key)).Returns(Owner);
                ApiScopeEvaluator = new ApiScopeEvaluator(userService.Object);
                BlobService = new Mock<IStagingBlobService>();
                BlobService.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
                ValidationEmitter = new Mock<IStagingValidationMessageEmitter>();
                MessageService = new Mock<IMessageService>();
                MessageServiceConfiguration = new Mock<IMessageServiceConfiguration>();
                AppConfiguration = new Mock<IAppConfiguration>();
                AppConfiguration.SetupGet(x => x.SiteRoot).Returns("https://nuget.test/");
                var dateTimeProvider = new Mock<IDateTimeProvider>();
                dateTimeProvider.SetupGet(x => x.UtcNow).Returns(_now);

                Target = new StagingService(
                    Entities,
                    PackageService.Object,
                    PackageUploadService.Object,
                    SymbolPackageService.Object,
                    ReservedNamespaceService.Object,
                    ApiScopeEvaluator,
                    BlobService.Object,
                    ValidationEmitter.Object,
                    dateTimeProvider.Object,
                    MessageService.Object,
                    MessageServiceConfiguration.Object,
                    AppConfiguration.Object,
                    new TestStagingTokenProtector());
            }

            public User CurrentUser { get; }
            public User Owner { get; }
            public Credential Credential { get; }
            public FakeEntitiesContext Entities { get; }
            public Mock<IDbContextTransaction> Transaction { get; }
            public Mock<IDatabase> Database { get; }
            public Mock<IPackageService> PackageService { get; }
            public Mock<IPackageUploadService> PackageUploadService { get; }
            public Mock<ISymbolPackageService> SymbolPackageService { get; }
            public Mock<IReservedNamespaceService> ReservedNamespaceService { get; }
            public IApiScopeEvaluator ApiScopeEvaluator { get; }
            public Mock<IStagingBlobService> BlobService { get; }
            public Mock<IStagingValidationMessageEmitter> ValidationEmitter { get; }
            public Mock<IMessageService> MessageService { get; }
            public Mock<IMessageServiceConfiguration> MessageServiceConfiguration { get; }
            public Mock<IAppConfiguration> AppConfiguration { get; }
            public StagingService Target { get; }

            public Task<StagingUploadResult> UploadAsync(StagingUploadRequest request)
            {
                return Target.UploadAsync(CurrentUser, Owner, Credential, request);
            }

            public PackageRegistration SetupExistingRegistration(string id)
            {
                var registration = new PackageRegistration { Key = 10, Id = id };
                registration.Owners.Add(Owner);
                PackageService.Setup(x => x.FindPackageRegistrationById(id)).Returns(registration);
                Entities.PackageRegistrations.Add(registration);
                return registration;
            }

            public void SetupPackageGeneration(string id, string version, int key)
            {
                PackageUploadService
                    .Setup(x => x.GeneratePackageAsync(id, It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), Owner, CurrentUser))
                    .ReturnsAsync((
                        string _,
                        PackageArchiveReader __,
                        PackageStreamMetadata metadata,
                        User ___,
                        User ____) =>
                    {
                        var registration = Entities.PackageRegistrations.Single(x => x.Id == id);
                        var package = new Package
                        {
                            Key = key,
                            Id = id,
                            Version = version,
                            NormalizedVersion = version,
                            PackageRegistration = registration,
                            Hash = metadata.Hash,
                            PackageStatusKey = PackageStatus.Staged,
                            User = CurrentUser,
                        };
                        registration.Packages.Add(package);
                        Entities.Packages.Add(package);
                        return package;
                    });
            }

            public void SetupBlob(StagingBlobType type, string path, string etag, string hash)
            {
                BlobService
                    .Setup(x => x.CreateAsync(It.IsAny<Stream>(), type))
                    .ReturnsAsync(new StagingBlobReference(path, etag, hash, 100, type));
            }

            public void SetupSymbolGeneration(Package parent, int key)
            {
                SymbolPackageService
                    .Setup(x => x.CreateSymbolPackage(parent, It.IsAny<PackageStreamMetadata>()))
                    .Returns((Package package, PackageStreamMetadata metadata) =>
                    {
                        var symbol = new SymbolPackage
                        {
                            Key = key,
                            Package = package,
                            PackageKey = package.Key,
                            Hash = metadata.Hash,
                            HashAlgorithm = metadata.HashAlgorithm,
                            StatusKey = PackageStatus.Staged,
                        };
                        package.SymbolPackages.Add(symbol);
                        Entities.SymbolPackages.Add(symbol);
                        return symbol;
                    });
            }

            public StagingGroup AddGroup(int key, string id)
            {
                var group = new StagingGroup
                {
                    Key = key,
                    Owner = Owner,
                    OwnerKey = Owner.Key,
                    Id = id,
                    Name = id,
                    CreatedDate = _now.AddDays(-1),
                    ExpirationDate = _now.AddDays(29),
                };
                Entities.StagingGroups.Add(group);
                return group;
            }

            public ExistingState AddExistingStagedPackage(string packageHash, string symbolHash)
            {
                var registration = new PackageRegistration { Key = 10, Id = "PackageA" };
                registration.Owners.Add(Owner);
                Entities.PackageRegistrations.Add(registration);
                var package = new Package
                {
                    Key = 42,
                    Id = "PackageA",
                    Version = "1.0.0",
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = registration,
                    Hash = packageHash,
                    PackageStatusKey = PackageStatus.Staged,
                    User = CurrentUser,
                };
                registration.Packages.Add(package);
                var entry = new StagingEntry
                {
                    Key = 50,
                    Package = package,
                    PackageKey = package.Key,
                    Owner = Owner,
                    OwnerKey = Owner.Key,
                    CreatedDate = _now.AddDays(-1),
                    ExpirationDate = _now.AddDays(29),
                    PackageArtifact = new StagedPackageArtifact
                    {
                        StagingEntryKey = 50,
                        BlobPath = "old-package.nupkg",
                        BlobETag = "old-package-etag",
                        ContentHash = packageHash,
                        Status = StagingArtifactStatus.Ready,
                        ValidationTrackingId = Guid.NewGuid(),
                        UploadedDate = _now.AddDays(-1),
                    },
                };
                entry.PackageArtifact.StagingEntry = entry;

                if (symbolHash != null)
                {
                    var symbolPackage = new SymbolPackage
                    {
                        Key = 43,
                        Package = package,
                        PackageKey = package.Key,
                        Hash = symbolHash,
                        HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                        StatusKey = PackageStatus.Staged,
                    };
                    package.SymbolPackages.Add(symbolPackage);
                    entry.SymbolArtifact = new StagedSymbolArtifact
                    {
                        StagingEntry = entry,
                        StagingEntryKey = entry.Key,
                        SymbolPackage = symbolPackage,
                        SymbolPackageKey = symbolPackage.Key,
                        BlobPath = "old-symbol.nupkg",
                        BlobETag = "old-symbol-etag",
                        ContentHash = symbolHash,
                        ParentContentHash = packageHash,
                        Status = StagingArtifactStatus.Ready,
                        ValidationTrackingId = Guid.NewGuid(),
                        UploadedDate = _now.AddDays(-1),
                    };
                    Entities.SymbolPackages.Add(symbolPackage);
                    Entities.StagedSymbolArtifacts.Add(entry.SymbolArtifact);
                }

                Entities.Packages.Add(package);
                Entities.StagingEntries.Add(entry);
                Entities.StagedPackageArtifacts.Add(entry.PackageArtifact);

                return new ExistingState(package, entry);
            }

            public Package AddParent(PackageStatus status, bool ownerOwns)
            {
                var registration = new PackageRegistration { Key = 10, Id = "PackageA" };
                registration.Owners.Add(ownerOwns ? Owner : new User { Key = 99 });
                var package = new Package
                {
                    Key = 42,
                    Id = "PackageA",
                    Version = "1.0.0",
                    NormalizedVersion = "1.0.0",
                    PackageRegistration = registration,
                    Hash = "parent-hash",
                    PackageStatusKey = status,
                    User = CurrentUser,
                };
                registration.Packages.Add(package);
                Entities.PackageRegistrations.Add(registration);
                Entities.Packages.Add(package);
                return package;
            }
        }

        private sealed class ExistingState
        {
            public ExistingState(Package package, StagingEntry entry)
            {
                Package = package;
                Entry = entry;
            }

            public Package Package { get; }
            public StagingEntry Entry { get; }
        }
    }
}
