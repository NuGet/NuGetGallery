// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a staged artifact returned by the public API.
    /// </summary>
    public class StagingArtifactResponse
    {
        private StagingArtifactResponse()
        {
        }

        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("version")]
        public string Version { get; private set; }

        [JsonProperty("kind")]
        public string Kind { get; private set; }

        [JsonProperty("owner")]
        public string Owner { get; private set; }

        [JsonProperty("group")]
        public StagingGroupReferenceResponse Group { get; private set; }

        [JsonProperty("status")]
        public string Status { get; private set; }

        [JsonProperty("uploaded")]
        public string Uploaded { get; private set; }

        [JsonProperty("validated")]
        public string Validated { get; private set; }

        [JsonProperty("expires")]
        public string Expires { get; private set; }

        [JsonProperty("listed", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Listed { get; private set; }

        [JsonProperty("canPromote")]
        public bool CanPromote { get; private set; }

        [JsonProperty("blockers")]
        public IReadOnlyList<StagingBlockerResponse> Blockers { get; private set; }

        [JsonProperty("managementUrl")]
        public string ManagementUrl { get; private set; }

        public static StagingArtifactResponse FromPackage(StagedPackage package, DateTime expirationDate, string managementUrl)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var isGrouped = package.StagedPackageIdentity.StagingGroupKey.HasValue;
            var isExpired = StagingExpirationPolicy.HasExpired(package);
            var ownershipBlocker = StagingOwnershipPolicy.GetBlocker(package.StagedPackageIdentity);
            var canPromote = package.Status == StagedPackageStatus.Ready && !isGrouped && !isExpired && ownershipBlocker == null;
            var blockers = new List<StagingBlockerResponse>();
            if (ownershipBlocker != null)
            {
                blockers.Add(ownershipBlocker);
            }

            if (isExpired)
            {
                blockers.Add(new StagingBlockerResponse("StagingExpired", "The staged package has expired. Delete the expired staging before uploading new content."));
            }
            if (isGrouped)
            {
                blockers.Add(new StagingBlockerResponse("PackageGrouped", "The staged package must be promoted with its group."));
            }
            else if (package.Status != StagedPackageStatus.Ready)
            {
                blockers.Add(new StagingBlockerResponse("PackageNotReady", "The staged package is not ready for promotion."));
            }

            return new StagingArtifactResponse
            {
                Id = package.StagedPackageIdentity.Package.PackageRegistration.Id,
                Version = package.StagedPackageIdentity.Package.NormalizedVersion,
                Kind = "package",
                Owner = package.StagedPackageIdentity.Owner.Username,
                Group = isGrouped ? new StagingGroupReferenceResponse(
                    package.StagedPackageIdentity.StagingGroup.Id,
                    package.StagedPackageIdentity.StagingGroup.Name) : null,
                Status = isExpired ? "expired" : GetStatus(package.Status),
                Uploaded = package.UploadedDate.ToUtcIso8601String(),
                // The authoritative validation completion timestamp will be persisted in a later unit.
                Validated = null,
                Expires = expirationDate.ToUtcIso8601String(),
                Listed = package.StagedPackageIdentity.Package.Listed,
                CanPromote = canPromote,
                Blockers = blockers,
                ManagementUrl = managementUrl,
            };
        }

        public static StagingArtifactResponse FromSymbolPackage(StagedSymbolPackage symbols, DateTime expirationDate, string managementUrl)
        {
            if (symbols == null)
            {
                throw new ArgumentNullException(nameof(symbols));
            }

            var identity = symbols.StagedPackageIdentity;
            var blockers = StagedSymbolPackagePromotionEligibility.GetBlockers(symbols);
            return new StagingArtifactResponse
            {
                Id = identity.Package.PackageRegistration.Id,
                Version = identity.Package.NormalizedVersion,
                Kind = "symbols",
                Owner = identity.Owner.Username,
                Group = identity.StagingGroupKey.HasValue ? new StagingGroupReferenceResponse(identity.StagingGroup.Id, identity.StagingGroup.Name) : null,
                Status = StagingExpirationPolicy.HasExpired(symbols) ? "expired" : GetStatus(symbols.Status),
                Uploaded = symbols.UploadedDate.ToUtcIso8601String(),
                Validated = null,
                Expires = expirationDate.ToUtcIso8601String(),
                Listed = null,
                CanPromote = blockers.Count == 0,
                Blockers = blockers,
                ManagementUrl = managementUrl,
            };
        }

        private static string GetStatus(StagedPackageStatus status)
        {
            switch (status)
            {
                case StagedPackageStatus.Validating:
                    return "validating";
                case StagedPackageStatus.WaitingForParent:
                    return "waitingForParent";
                case StagedPackageStatus.Ready:
                    return "ready";
                case StagedPackageStatus.FailedValidation:
                    return "validationFailed";
                case StagedPackageStatus.Promoting:
                    return "promoting";
                case StagedPackageStatus.Succeeded:
                    return "succeeded";
                case StagedPackageStatus.PromotionFailed:
                    return "promotionFailed";
                default:
                    throw new NotImplementedException($"The staged package status '{status}' is not part of the live API contract.");
            }
        }
    }

    /// <summary>
    /// Identifies a staging group associated with an artifact.
    /// </summary>
    public class StagingGroupReferenceResponse
    {
        public StagingGroupReferenceResponse(string id, string name)
        {
            Id = id;
            Name = name;
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("name")]
        public string Name { get; }
    }
}
