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

        [JsonProperty("listed")]
        public bool Listed { get; private set; }

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
            StagingGroupReferenceResponse group = null;
            if (isGrouped)
            {
                group = new StagingGroupReferenceResponse(package.StagedPackageIdentity.StagingGroup.Id, package.StagedPackageIdentity.StagingGroup.Name);
            }

            var canPromote = package.Status == StagedPackageStatus.Ready && !isGrouped;
            var blockers = new List<StagingBlockerResponse>();
            if (isGrouped)
            {
                blockers.Add(new StagingBlockerResponse("PackageGrouped", "The staged package must be promoted with its group."));
            }
            else if (!canPromote)
            {
                blockers.Add(new StagingBlockerResponse("PackageNotReady", "The staged package is not ready for promotion."));
            }

            return new StagingArtifactResponse
            {
                Id = package.StagedPackageIdentity.Package.PackageRegistration.Id,
                Version = package.StagedPackageIdentity.Package.NormalizedVersion,
                Kind = "package",
                Owner = package.StagedPackageIdentity.Owner.Username,
                Group = group,
                Status = GetStatus(package.Status),
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

        private static string GetStatus(StagedPackageStatus status)
        {
            switch (status)
            {
                case StagedPackageStatus.Validating:
                    return "validating";
                case StagedPackageStatus.Ready:
                    return "ready";
                case StagedPackageStatus.FailedValidation:
                    return "validationFailed";
                case StagedPackageStatus.Promoting:
                    return "promoting";
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
