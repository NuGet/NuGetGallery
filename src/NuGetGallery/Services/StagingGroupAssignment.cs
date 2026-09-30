// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Updates shared group membership and fences group mutations during staged uploads.
    /// </summary>
    internal static class StagingGroupAssignment
    {
        internal static void Update(StagedPackageIdentity identity, StagingGroup requestedGroup, IEntityRepository<StagingGroup> groups)
        {
            var previousGroup = identity.StagingGroup;
            if (previousGroup != null)
            {
                previousGroup.MutationRevision++;
            }

            if (requestedGroup != null && requestedGroup.Key != previousGroup?.Key)
            {
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
