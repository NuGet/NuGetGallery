// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Separates access to private staging from the owner's current permission to publish or upload content.
    /// </summary>
    internal static class StagingOwnershipPolicy
    {
        internal const string BlockerMessage = "The staging owner no longer owns this package ID. Restore registration ownership before uploading new content or promoting. You can still download or delete the staged content.";

        internal static bool CanPublish(StagedPackageIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return identity.Package.PackageRegistration.Owners.Any(owner => owner.Key == identity.OwnerKey);
        }

        internal static StagingBlockerResponse GetBlocker(StagedPackageIdentity identity)
        {
            if (CanPublish(identity))
            {
                return null;
            }

            return new StagingBlockerResponse("RegistrationOwnershipLost", BlockerMessage);
        }
    }
}
