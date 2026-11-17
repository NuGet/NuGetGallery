// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    /// <summary>
    /// Resolves upload groups, updates shared membership, and fences group mutations during staged uploads.
    /// </summary>
    internal static class StagingGroupAssignment
    {
        internal static PackageStagingResult ResolveForUpload(User owner, string groupId, IPackageStagingManagementService managementService, IAppConfiguration configuration, out StagingGroup group)
        {
            group = null;
            if (groupId == null)
            {
                return null;
            }

            group = managementService.FindStagingGroup(owner, groupId);
            if (group == null)
            {
                group = new StagingGroup
                {
                    Owner = owner,
                    OwnerKey = owner.Key,
                    Id = groupId,
                    Name = groupId,
                    CreatedDate = DateTime.UtcNow,
                    ExpirationDate = StagingExpirationPolicy.CreateDeadline(configuration),
                };
            }

            if (group.ActivePromotionId.HasValue)
            {
                return PackageStagingResult.Error(HttpStatusCode.Conflict, "The staging group is being promoted.");
            }

            return null;
        }

        internal static void Update(StagedPackageIdentity identity, StagingGroup requestedGroup, IEntityRepository<StagingGroup> groups, DateTime membershipDeadline)
        {
            var previousGroup = identity.StagingGroup;
            if (previousGroup != null)
            {
                previousGroup.MutationRevision++;
            }

            if (requestedGroup != null && requestedGroup.Key != previousGroup?.Key)
            {
                StagingExpirationPolicy.EnsureMutable(identity, requestedGroup);
                StagingExpirationPolicy.RefreshGroup(previousGroup, membershipDeadline);
                StagingExpirationPolicy.RefreshGroup(requestedGroup, membershipDeadline);

                if (requestedGroup.Key == 0)
                {
                    groups.InsertOnCommit(requestedGroup);
                }
                else
                {
                    requestedGroup.MutationRevision++;
                    identity.StagingGroupKey = requestedGroup.Key;
                }

                identity.StagingGroup = requestedGroup;
            }
        }
    }
}
