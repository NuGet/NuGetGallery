// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGetGallery.Infrastructure.Mail
{
    public static class EmailSubjectBuilder
    {
        public static string ForReportAbuse(string galleryOwnerDisplayName, string packageId, string packageVersion, string reason)
        {
            return $"[{galleryOwnerDisplayName}] Support Request for '{packageId}' version {packageVersion} (Reason: {reason})";
        }

        public static string ForReportMyPackage(string galleryOwnerDisplayName, string packageId, string packageVersion, string reason)
        {
            return $"[{galleryOwnerDisplayName}] Owner Support Request for '{packageId}' version {packageVersion} (Reason: {reason})";
        }

        public static string ForSendContactOwnersMessage(string packageRegistrationId, string galleryOwnerDisplayName)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Message for owners of the package '{1}'",
                galleryOwnerDisplayName,
                packageRegistrationId);
        }

        public static string ForSendNewAccountEmail(string galleryOwnerDisplayName)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Please verify your account",
                galleryOwnerDisplayName);
        }

        public static string ForSendAccountDeleteNotice()
        {
            return CoreStrings.AccountDelete_SupportRequestTitle;
        }

        public static string ForSendPackageAddedNotice(string galleryOwnerDisplayName, Package package, bool hasWarnings)
        {
            if (hasWarnings)
            {
                return $"[{galleryOwnerDisplayName}] Package published with warnings - {package.PackageRegistration.Id} {package.Version}";
            }
            else
            {
                return $"[{galleryOwnerDisplayName}] Package published - {package.PackageRegistration.Id} {package.Version}";
            }
        }

        public static string ForSendContactSupportEmail(string reason)
        {
            return string.Format(CultureInfo.CurrentCulture, "Support Request (Reason: {0})", reason);
        }

        public static string ForSendEmailChangeConfirmationNotice(string galleryOwnerDisplayName, bool isOrganization)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Please verify your {1}'s new email address",
                galleryOwnerDisplayName,
                isOrganization ? "organization" : "account");
        }

        public static string ForSendEmailChangeNoticeToPreviousEmailAddress(string galleryOwnerDisplayName, bool isOrganization)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Recent changes to your {1}'s email",
                galleryOwnerDisplayName,
                isOrganization ? "organization" : "account");
        }

        public static string ForSendSymbolPackageAddedNotice(string galleryOwnerDisplayName, bool hasWarnings, SymbolPackage symbolPackage)
        {
            string subject;
            if (hasWarnings)
            {
                subject = $"[{galleryOwnerDisplayName}] Symbol package published with warnings - {symbolPackage.Id} {symbolPackage.Version}";
            }
            else
            {
                subject = $"[{galleryOwnerDisplayName}] Symbol package published - {symbolPackage.Id} {symbolPackage.Version}";
            }
            return subject;
        }

        public static string ForSendOrganizationMemberRemovedNotice(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership update for organization '{organization.Username}'";
        }

        public static string ForSendOrganizationMembershipRequest(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership request for organization '{organization.Username}'";
        }

        public static string ForSendOrganizationMembershipRequestCancelledNotice(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership request for organization '{organization.Username}' cancelled";
        }

        public static string ForSendOrganizationMembershipRequestInitiatedNotice(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership request for organization '{organization.Username}'";
        }

        public static string ForSendOrganizationMembershipRequestRejectedNotice(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership request for organization '{organization.Username}' declined";
        }

        public static string ForSendOrganizationMemberUpdatedNotice(string galleryOwnerDisplayName, Organization organization)
        {
            return $"[{galleryOwnerDisplayName}] Membership update for organization '{organization.Username}'";
        }

        public static string ForSendOrganizationTransformInitiatedNotice(string galleryOwnerDisplayName, User accountToTransform)
        {
            return $"[{galleryOwnerDisplayName}] Organization transformation for account '{accountToTransform.Username}'";
        }

        public static string ForSendPackageAddedWithWarningsNotice(string galleryOwnerDisplayName, Package package)
        {
            return $"[{galleryOwnerDisplayName}] Package pushed with warnings - {package.PackageRegistration.Id} {package.Version}";
        }

        public static string ForSendOrganizationTransformRequestAcceptedNotice(string galleryOwnerDisplayName, User accountToTransform)
        {
            return $"[{galleryOwnerDisplayName}] Account '{accountToTransform.Username}' has been transformed into an organization";
        }

        public static string ForSendSymbolPackageValidationFailedNotice(string galleryOwnerDisplayName, SymbolPackage symbolPackage)
        {
            return $"[{galleryOwnerDisplayName}] Symbol package validation failed - {symbolPackage.Id} {symbolPackage.Version}";
        }

        public static string ForSendOrganizationTransformRequest(string galleryOwnerDisplayName, User accountToTransform)
        {
            return $"[{galleryOwnerDisplayName}] Organization transformation for account '{accountToTransform.Username}'";
        }

        public static string ForSendOrganizationTransformRequestRejectedNotice(string galleryOwnerDisplayName, User accountToTransform)
        {
            return $"[{galleryOwnerDisplayName}] Transformation of account '{accountToTransform.Username}' has been cancelled";
        }

        public static string ForSendValidationTakingTooLongNotice(string galleryOwnerDisplayName, Package package)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Package validation taking longer than expected - {1} {2}",
                galleryOwnerDisplayName,
                package.PackageRegistration.Id,
                package.Version);
        }

        public static string ForSendPackageValidationFailedNotice(string galleryOwnerDisplayName, Package package)
        {
            return $"[{galleryOwnerDisplayName}] Package validation failed - {package.PackageRegistration.Id} {package.Version}";
        }

        public static string ForSendPackageDeletedNotice(string galleryOwnerDisplayName, Package package)
        {
            return string.Format(
                   CultureInfo.CurrentCulture,
                   "[{0}] Package deleted - {1} {2}",
                   galleryOwnerDisplayName,
                   package.PackageRegistration.Id,
                   package.Version);
        }

        public static string ForSendValidationTakingTooLongNotice(string galleryOwnerDisplayName, SymbolPackage symbolPackage)
        {
            return string.Format(
                   CultureInfo.CurrentCulture,
                   "[{0}] Symbol package validation taking longer than expected - {1} {2}",
                   galleryOwnerDisplayName,
                   symbolPackage.Id,
                   symbolPackage.Version);
        }

        public static string ForSendPackageOwnerAddedNotice(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership update for '{package.Id}'";
        }

        public static string ForSendPackageOwnerRemovedNotice(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership removal for '{package.Id}'";
        }

        public static string ForSendPackageOwnerRequest(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership request for '{package.Id}'";
        }

        public static string ForSendPackageOwnerRequestCancellationNotice(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership request for '{package.Id}' cancelled";
        }

        public static string ForSendPackageOwnerRequestInitiatedNotice(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership request for '{package.Id}'";
        }

        public static string ForSendPackageOwnerRequestRejectionNotice(string galleryOwnerDisplayName, PackageRegistration package)
        {
            return $"[{galleryOwnerDisplayName}] Package ownership request for '{package.Id}' declined";
        }

        public static string ForSendPasswordResetInstructions(string galleryOwnerDisplayName, bool forgotPassword)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? CoreStrings.Emails_ForgotPassword_Subject : CoreStrings.Emails_SetPassword_Subject,
                galleryOwnerDisplayName);
        }
    }
}