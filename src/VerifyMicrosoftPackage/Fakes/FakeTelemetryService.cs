// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Principal;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.VerifyMicrosoftPackage.Fakes
{
    public class FakeTelemetryService : ITelemetryService
    {
        public void TraceException(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void TrackAccountDeletionCompleted(User deletedUser, User deletedBy, bool success)
        {
            throw new NotImplementedException();
        }

        public void TrackCertificateActivated(string thumbprint)
        {
            throw new NotImplementedException();
        }

        public void TrackCertificateAdded(string thumbprint)
        {
            throw new NotImplementedException();
        }

        public void TrackCertificateDeactivated(string thumbprint)
        {
            throw new NotImplementedException();
        }

        public void TrackCreatePackageVerificationKeyEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            throw new NotImplementedException();
        }

        public void TrackException(Exception exception, Action<Dictionary<string, string>> addProperties)
        {
            throw new NotImplementedException();
        }

        public void TrackInvalidLicenseMetadata(string licenseValue)
        {
            throw new NotImplementedException();
        }

        public void TrackLicenseFileRejected()
        {
            throw new NotImplementedException();
        }

        public void TrackLicenseValidationFailure()
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForSearchCircuitBreakerOnBreak(string searchName, Exception exception, HttpResponseMessage responseMessage, string correlationId, string uri)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForSearchCircuitBreakerOnReset(string searchName, string correlationId, string uri)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForSearchExecutionDuration(string url, TimeSpan duration, bool success)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForSearchOnRetry(string searchName, Exception exception, string correlationId, string uri, string circuitBreakerStatus)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForTyposquattingAlgorithmProcessingTime(string packageId, TimeSpan algorithmProcessingTime)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForTyposquattingChecklistRetrievalTime(string packageId, TimeSpan checklistRetrievalTime)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForTyposquattingCheckResultAndTotalTime(string packageId, TimeSpan totalTime, bool wasUploadBlocked, List<string> collisionPackageIds, int checkListLength, TimeSpan checkListCacheExpireTime)
        {
            throw new NotImplementedException();
        }

        public void TrackMetricForTyposquattingOwnersCheckTime(string packageId, TimeSpan ownersCheckTime)
        {
            throw new NotImplementedException();
        }

        public void TrackNewCredentialCreated(User user, Credential credential)
        {
            throw new NotImplementedException();
        }

        public void TrackNewUserRegistrationEvent(User user, Credential identity)
        {
            throw new NotImplementedException();
        }

        public void TrackNonFsfOsiLicenseUse(string licenseExpression)
        {
            throw new NotImplementedException();
        }

        public void TrackODataCustomQuery(bool? customQuery)
        {
            throw new NotImplementedException();
        }

        public void TrackODataQueryFilterEvent(string callContext, bool isEnabled, bool isAllowed, string queryPattern)
        {
            throw new NotImplementedException();
        }

        public void TrackOrganizationAdded(Organization organization)
        {
            throw new NotImplementedException();
        }

        public void TrackOrganizationTransformCancelled(User user)
        {
            throw new NotImplementedException();
        }

        public void TrackOrganizationTransformCompleted(User user)
        {
            throw new NotImplementedException();
        }

        public void TrackOrganizationTransformDeclined(User user)
        {
            throw new NotImplementedException();
        }

        public void TrackOrganizationTransformInitiated(User user)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageDelete(Package package, bool isHardDelete)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageHardDeleteReflow(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageListed(Package package)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageMetadataComplianceError(string packageId, string packageVersion, IEnumerable<string> complianceFailures)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageMetadataComplianceWarning(string packageId, string packageVersion, IEnumerable<string> complianceWarnings)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageOwnershipAutomaticallyAdded(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackPackagePushEvent(Package package, User user, IIdentity identity)
        {
            throw new NotImplementedException();
        }

        public void TrackPackagePushFailureEvent(string id, NuGetVersion version)
        {
            throw new NotImplementedException();
        }

        public void TrackPackagePushNamespaceConflictEvent(string packageId, string packageVersion, User user, IIdentity identity)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageReadMeChangeEvent(Package package, string readMeSourceType, PackageEditReadMeState readMeState)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageReflow(Package package)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageReupload(Package package)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageRevalidate(Package package)
        {
            throw new NotImplementedException();
        }

        public void TrackPackageUnlisted(Package package)
        {
            throw new NotImplementedException();
        }

        public void TrackRequestForAccountDeletion(User user)
        {
            throw new NotImplementedException();
        }

        public void TrackRequiredSignerSet(string packageId)
        {
            throw new NotImplementedException();
        }

        public void TrackSendEmail(string smtpUri, DateTimeOffset startTime, TimeSpan duration, bool success, int attemptNumber)
        {
            throw new NotImplementedException();
        }

        public void TrackSymbolPackageDeleteEvent(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackSymbolPackageFailedGalleryValidationEvent(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackSymbolPackagePushEvent(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackSymbolPackagePushFailureEvent(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackSymbolPackageRevalidate(string packageId, string packageVersion)
        {
            throw new NotImplementedException();
        }

        public void TrackUserChangedMultiFactorAuthentication(User user, bool enabledMultiFactorAuth)
        {
            throw new NotImplementedException();
        }

        public void TrackUserLogin(User user, Credential credential, bool wasMultiFactorAuthenticated)
        {
            throw new NotImplementedException();
        }

        public void TrackUserPackageDeleteChecked(UserPackageDeleteEvent details, UserPackageDeleteOutcome outcome)
        {
            throw new NotImplementedException();
        }

        public void TrackUserPackageDeleteExecuted(int packageKey, string packageId, string packageVersion, ReportPackageReason reason, bool success)
        {
            throw new NotImplementedException();
        }

        public void TrackVerifyPackageKeyEvent(string packageId, string packageVersion, User user, IIdentity identity, int statusCode)
        {
            throw new NotImplementedException();
        }
    }
}
