// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    internal static class StagingResourceBuilder
    {
        public static StagedPackageResource CreatePackage(StagingEntry entry, User owner, StagingArtifactOperation? packageOperation, StagingArtifactOperation? symbolsOperation)
        {
            var package = CreatePackageArtifact(entry, packageOperation);

            StagingArtifactResource symbols = null;
            if (entry.SymbolArtifact != null)
            {
                symbols = CreateArtifact(entry.SymbolArtifact, symbolsOperation);
            }

            return new StagedPackageResource
            {
                Id = entry.Package.PackageRegistration.Id,
                Version = entry.Package.NormalizedVersion,
                Owner = owner.Username,
                Created = entry.CreatedDate,
                Expires = entry.StagingGroup?.ExpirationDate ?? entry.ExpirationDate,
                Group = CreateGroupReference(entry.StagingGroup),
                Status = GetStagedPackageStatus(package, symbols),
                CanPromote = false,
                Blockers = Array.Empty<StagingBlockerResource>(),
                Package = package,
                Symbols = symbols,
            };
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

        private static string GetStagedPackageStatus(StagingArtifactResource package, StagingArtifactResource symbols)
        {
            if (package.Status == StagingResourceValues.StatusValidationFailed || symbols?.Status == StagingResourceValues.StatusValidationFailed)
            {
                return StagingResourceValues.StatusValidationFailed;
            }

            if (package.Status == StagingResourceValues.StatusValidating || symbols?.Status == StagingResourceValues.StatusValidating)
            {
                return StagingResourceValues.StatusValidating;
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
            return new StagingArtifactResource
            {
                Source = StagingResourceValues.SourceStaged,
                Status = GetArtifactStatus(status),
                Uploaded = uploaded,
                Validated = validated,
                Operation = GetOperationValue(operation),
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
