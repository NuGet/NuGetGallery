// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    internal static class StagingResourceBuilder
    {
        public static StagedPackageResource CreatePackage(
            StagingEntry entry,
            User owner,
            string galleryUrl,
            string promotionGalleryUrl,
            StagingArtifactOperation? packageOperation = null,
            StagingArtifactOperation? symbolsOperation = null)
        {
            var package = CreatePackageArtifact(entry, packageOperation);

            StagingArtifactResource symbols = null;
            if (entry.SymbolArtifact != null)
            {
                symbols = CreateArtifact(entry.SymbolArtifact, symbolsOperation);
            }

            var ownershipRemoved = IsOwnershipRemoved(entry, owner);
            var blockers = BuildBlockers(entry, package, symbols, ownershipRemoved);
            var status = GetStagedPackageStatus(package, symbols, ownershipRemoved);
            var canPromote = status == StagingResourceValues.StatusReady && blockers.Count == 0;

            return new StagedPackageResource
            {
                Id = entry.Package.PackageRegistration.Id,
                Version = entry.Package.NormalizedVersion,
                Owner = owner.Username,
                Created = entry.CreatedDate,
                Expires = entry.StagingGroup?.ExpirationDate ?? entry.ExpirationDate,
                Group = CreateGroupReference(entry.StagingGroup),
                Status = status,
                CanPromote = canPromote,
                Blockers = blockers,
                Package = package,
                Symbols = symbols,
                Promotion = CreatePromotion(entry, package, symbols, promotionGalleryUrl),
                GalleryUrl = galleryUrl,
            };
        }

        public static StagingGroupResource CreateGroupSummary(
            StagingGroup group,
            User owner,
            int packageCount,
            int artifactCount,
            string status,
            string galleryUrl)
        {
            var canPromote = packageCount > 0 && status == StagingResourceValues.StatusReady;

            return new StagingGroupResource
            {
                Id = group.Id,
                Name = group.Name,
                Owner = owner.Username,
                Created = group.CreatedDate,
                Expires = group.ExpirationDate,
                PackageCount = packageCount,
                ArtifactCount = artifactCount,
                Status = status,
                CanPromote = canPromote,
                GalleryUrl = galleryUrl,
            };
        }

        private static bool IsOwnershipRemoved(StagingEntry entry, User owner)
        {
            // Zero owners is treated as ownership removed too: an ownerless registration cannot be promoted by this
            // owner, so the staged package is blocked rather than ready.
            var owners = entry.Package?.PackageRegistration?.Owners;
            return owners == null || !owners.Any(x => x.Key == owner.Key);
        }

        private static IReadOnlyList<StagingBlockerResource> BuildBlockers(
            StagingEntry entry,
            StagingArtifactResource package,
            StagingArtifactResource symbols,
            bool ownershipRemoved)
        {
            var blockers = new List<StagingBlockerResource>();

            if (package.Source == StagingResourceValues.SourceStaged)
            {
                AddArtifactStateBlocker(blockers, package.Status, "package");
            }
            else
            {
                AddReferenceBlocker(blockers, package.Status);
            }

            if (symbols != null)
            {
                AddArtifactStateBlocker(blockers, symbols.Status, "symbols");
            }

            if (ownershipRemoved)
            {
                blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.OwnershipRemoved, Message = "The owner no longer owns this package registration." });
            }

            if (entry.StagingGroupKey != null)
            {
                blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.GroupedPackage, Message = "Promote the containing group to promote this staged package." });
            }

            return blockers;
        }

        private static void AddArtifactStateBlocker(List<StagingBlockerResource> blockers, string status, string artifact)
        {
            if (status == StagingResourceValues.StatusValidationFailed)
            {
                var code = artifact == "symbols" ? StagingBlockerCodes.SymbolValidationFailed : StagingBlockerCodes.PackageValidationFailed;
                var message = artifact == "symbols" ? "Symbol validation failed." : "Package validation failed.";
                blockers.Add(new StagingBlockerResource { Code = code, Artifact = artifact, Message = message });
            }
            else if (status == StagingResourceValues.StatusPromotionFailed)
            {
                blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.PromotionFailed, Artifact = artifact, Message = "Promotion failed; recover it from the Gallery." });
            }
        }

        private static void AddReferenceBlocker(List<StagingBlockerResource> blockers, string referenceStatus)
        {
            switch (referenceStatus)
            {
                case StagingResourceValues.StatusValidating:
                    blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.ParentValidating, Artifact = "package", Message = "The referenced parent package is still validating." });
                    break;
                case StagingResourceValues.StatusValidationFailed:
                    blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.ParentValidationFailed, Artifact = "package", Message = "The referenced parent package failed validation." });
                    break;
                case StagingResourceValues.StatusDeleted:
                    blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.ParentDeleted, Artifact = "package", Message = "The referenced parent package was deleted." });
                    break;
                case StagingResourceValues.StatusBlocked:
                    blockers.Add(new StagingBlockerResource { Code = StagingBlockerCodes.ParentNotFound, Artifact = "package", Message = "The referenced parent package is not available." });
                    break;
            }
        }

        private static StagingArtifactResource CreatePackageArtifact(StagingEntry entry, StagingArtifactOperation? operation)
        {
            if (entry.PackageArtifact != null)
            {
                return CreateArtifact(entry.PackageArtifact, operation);
            }

            return new StagingArtifactResource
            {
                Source = StagingResourceValues.SourceReference,
                Status = GetReferenceStatus(entry.Package),
            };
        }

        private static StagingGroupReferenceResource CreateGroupReference(StagingGroup group)
        {
            if (group == null)
            {
                return null;
            }

            return new StagingGroupReferenceResource
            {
                Id = group.Id,
                Name = group.Name,
            };
        }

        private static StagingPromotionResource CreatePromotion(StagingEntry entry, StagingArtifactResource package, StagingArtifactResource symbols, string promotionGalleryUrl)
        {
            var isPromoting = package.Status == StagingResourceValues.StatusPromoting || symbols?.Status == StagingResourceValues.StatusPromoting;
            var isPromotionFailed = package.Status == StagingResourceValues.StatusPromotionFailed || symbols?.Status == StagingResourceValues.StatusPromotionFailed;
            if (!isPromoting && !isPromotionFailed)
            {
                return null;
            }

            return new StagingPromotionResource
            {
                Status = isPromoting ? StagingResourceValues.StatusPromoting : StagingResourceValues.StatusPromotionFailed,
                Started = GetPromotionStarted(entry),
                GalleryUrl = promotionGalleryUrl,
            };
        }

        private static DateTime? GetPromotionStarted(StagingEntry entry)
        {
            var packageStarted = entry.PackageArtifact?.PromotionStartedDate;
            var symbolStarted = entry.SymbolArtifact?.PromotionStartedDate;
            if (packageStarted.HasValue && symbolStarted.HasValue)
            {
                return packageStarted.Value <= symbolStarted.Value ? packageStarted : symbolStarted;
            }

            return packageStarted ?? symbolStarted;
        }

        private static string GetStagedPackageStatus(StagingArtifactResource package, StagingArtifactResource symbols, bool ownershipRemoved)
        {
            bool AnyPanelIs(string status) => package.Status == status || symbols?.Status == status;

            if (AnyPanelIs(StagingResourceValues.StatusPromoting))
            {
                return StagingResourceValues.StatusPromoting;
            }

            if (AnyPanelIs(StagingResourceValues.StatusPromotionFailed))
            {
                return StagingResourceValues.StatusPromotionFailed;
            }

            if (AnyPanelIs(StagingResourceValues.StatusValidationFailed))
            {
                return StagingResourceValues.StatusValidationFailed;
            }

            if (AnyPanelIs(StagingResourceValues.StatusValidating))
            {
                return StagingResourceValues.StatusValidating;
            }

            var referenceBlocksPromotion = package.Source == StagingResourceValues.SourceReference
                && (package.Status == StagingResourceValues.StatusDeleted || package.Status == StagingResourceValues.StatusBlocked);
            if (ownershipRemoved || referenceBlocksPromotion)
            {
                return StagingResourceValues.StatusBlocked;
            }

            return StagingResourceValues.StatusReady;
        }

        private static StagingArtifactResource CreateArtifact(StagedPackageArtifact artifact, StagingArtifactOperation? operation)
        {
            return CreateArtifact(artifact.Status, artifact.UploadedDate, artifact.ValidatedDate, operation);
        }

        private static StagingArtifactResource CreateArtifact(StagedSymbolArtifact artifact, StagingArtifactOperation? operation)
        {
            return CreateArtifact(artifact.Status, artifact.UploadedDate, artifact.ValidatedDate, operation);
        }

        private static StagingArtifactResource CreateArtifact(StagingArtifactStatus status, DateTime uploaded, DateTime? validated, StagingArtifactOperation? operation)
        {
            // DEFERRED (Unit 9): artifact validation-error details are intentionally omitted. The Gallery entity model
            // does not persist them and staging must not read the separate validation database cross-database, so
            // `errors` is always left null (and omitted from the response) rather than emitting an empty collection
            // that would imply complete findings. Surfacing real findings is a later contract addition.
            return new StagingArtifactResource
            {
                Source = StagingResourceValues.SourceStaged,
                Status = GetArtifactStatus(status),
                Uploaded = uploaded,
                Validated = validated,
                Operation = GetOperationValue(operation),
                Errors = null,
            };
        }

        private static string GetOperationValue(StagingArtifactOperation? operation)
        {
            switch (operation)
            {
                case StagingArtifactOperation.Created:
                    return StagingResourceValues.OperationCreated;
                case StagingArtifactOperation.Replaced:
                    return StagingResourceValues.OperationReplaced;
                case StagingArtifactOperation.Unchanged:
                    return StagingResourceValues.OperationUnchanged;
                case null:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        private static string GetArtifactStatus(StagingArtifactStatus status)
        {
            switch (status)
            {
                case StagingArtifactStatus.Validating:
                    return StagingResourceValues.StatusValidating;
                case StagingArtifactStatus.Ready:
                    return StagingResourceValues.StatusReady;
                case StagingArtifactStatus.ValidationFailed:
                    return StagingResourceValues.StatusValidationFailed;
                case StagingArtifactStatus.Promoting:
                    return StagingResourceValues.StatusPromoting;
                case StagingArtifactStatus.PromotionFailed:
                    return StagingResourceValues.StatusPromotionFailed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static string GetReferenceStatus(Package package)
        {
            switch (package.PackageStatusKey)
            {
                case PackageStatus.Available:
                    return StagingResourceValues.StatusAvailable;
                case PackageStatus.Validating:
                    return StagingResourceValues.StatusValidating;
                case PackageStatus.FailedValidation:
                    return StagingResourceValues.StatusValidationFailed;
                case PackageStatus.Deleted:
                    return StagingResourceValues.StatusDeleted;
                default:
                    return StagingResourceValues.StatusBlocked;
            }
        }
    }
}
