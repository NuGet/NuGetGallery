// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Applies publishing restrictions without changing access to private staged content.
    /// </summary>
    public static class StagingPublicationPolicy
    {
        public static StagingPublicationBlocker GetRequesterBlocker(User currentUser)
        {
            if (currentUser == null)
            {
                throw new ArgumentNullException(nameof(currentUser));
            }

            if (currentUser.IsLocked)
            {
                return new StagingPublicationBlocker("RequestingUserLocked", "Your account is locked. Contact support before promoting staged content.");
            }

            if (!currentUser.Confirmed)
            {
                return new StagingPublicationBlocker("RequestingUserUnconfirmed", "Confirm your email address before promoting staged content.");
            }

            return null;
        }

        public static StagingPublicationBlocker GetOwnerBlocker(User owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (owner.IsLocked)
            {
                return new StagingPublicationBlocker("StagingOwnerLocked", "The staging owner is locked. Contact support before promoting staged content.");
            }

            if (!owner.Confirmed)
            {
                return new StagingPublicationBlocker("StagingOwnerUnconfirmed", "Confirm the staging owner's email address before promoting staged content.");
            }

            return null;
        }

        public static StagingPublicationBlocker GetBlocker(StagedPackageIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            var ownerBlocker = GetOwnerBlocker(identity.Owner);
            if (ownerBlocker != null)
            {
                return ownerBlocker;
            }

            var registration = identity.Package.PackageRegistration;
            if (registration.IsLocked)
            {
                return new StagingPublicationBlocker("PackageRegistrationLocked", "The package ID is locked. Contact support before promoting staged content.");
            }

            if (!registration.Owners.Any(owner => owner.Key == identity.OwnerKey))
            {
                return new StagingPublicationBlocker("RegistrationOwnershipLost", "The staging owner no longer owns this package ID. Restore registration ownership before uploading new content or promoting. You can still download or delete the staged content.");
            }

            return null;
        }
    }
}
