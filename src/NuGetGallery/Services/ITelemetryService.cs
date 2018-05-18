﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Principal;

namespace NuGetGallery
{
    public interface ITelemetryService
    {
        void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern);

        void TrackPackagePushEvent(Package package, User user, IIdentity identity);

        void TrackPackageUnlisted(Package package);

        void TrackPackageListed(Package package);

        void TrackPackageDelete(Package package, bool isHardDelete);

        void TrackPackageReflow(Package package);

        void TrackPackageHardDeleteReflow(string packageId, string packageVersion);

        void TrackPackageRevalidate(Package package);

        void TrackPackageReadMeChangeEvent(Package package, string readMeSourceType, PackageEditReadMeState readMeState);

        void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity);

        void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity);

        void TrackVerifyPackageKeyEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode);

        void TrackNewUserRegistrationEvent(User user, Credential identity);

        void TrackUserChangedMultiFactorAuthentication(User user, bool enabledMultiFactorAuth);

        void TrackNewCredentialCreated(User user, Credential credential);

        /// <summary>
        /// A telemetry event emitted when the service checks whether a user package delete is allowed.
        /// </summary>
        void TrackUserPackageDeleteChecked(UserPackageDeleteEvent details, UserPackageDeleteOutcome outcome);

        /// <summary>
        /// A telemetry event emitted when a user package delete is executed.
        /// </summary>
        void TrackUserPackageDeleteExecuted(int packageKey, string packageId, string packageVersion, ReportPackageReason reason, bool success);

        /// <summary>
        /// A telemetry event emitted when a certificate is added to the database.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        void TrackCertificateAdded(string thumbprint);

        /// <summary>
        /// A telemetry event emitted when a certificate is activated for an account.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        void TrackCertificateActivated(string thumbprint);

        /// <summary>
        /// A telemetry event emitted when a certificate is deactivated for an account.
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        void TrackCertificateDeactivated(string thumbprint);

        /// <summary>
        /// A telemetry event emitted when the required signer is set on a package registration.
        /// </summary>
        /// <param name="packageId">The package ID.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="thumbprint" /> is <c>null</c>
        /// or empty.</exception>
        void TrackRequiredSignerSet(string packageId);

        /// <summary>
        /// A telemetry event emitted when a user requests transformation of their account into an organization.
        /// </summary>
        void TrackOrganizationTransformInitiated(User user);

        /// <summary>
        /// A telemetry event emitted when a user completes transformation of their account into an organization.
        /// </summary>
        void TrackOrganizationTransformCompleted(User user);

        /// <summary>
        /// A telemetry event emitted when a user's request to transform their account into an organization is declined.
        /// </summary>
        void TrackOrganizationTransformDeclined(User user);

        /// <summary>
        /// A telemetry event emitted when a user cancels their request to transform their account into an organization.
        /// </summary>
        void TrackOrganizationTransformCancelled(User user);

        /// <summary>
        /// A telemetry event emitted when a user adds a new organization to their account.
        /// </summary>
        void TrackOrganizationAdded(Organization organization);

        /// <summary>
        /// Create a trace for an exception. These are informational for support requests.
        /// </summary>
        void TraceException(Exception exception);

        /// <summary>
        /// Create a log for an exception. These are warnings for live site.
        /// </summary>
        void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties);
    }
}