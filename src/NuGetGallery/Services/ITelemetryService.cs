// Copyright (c) .NET Foundation. All rights reserved.
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

        void TrackPackageReadMeChangeEvent(Package package, string readMeSourceType, PackageEditReadMeState readMeState);

        void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity);

        void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity);

        void TrackVerifyPackageKeyEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode);

        void TrackNewUserRegistrationEvent(User user, Credential identity);

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
        /// Create a trace for an exception. These are informational for support requests.
        /// </summary>
        void TraceException(Exception exception);

        /// <summary>
        /// Create a log for an exception. These are warnings for live site.
        /// </summary>
        void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties);
    }
}