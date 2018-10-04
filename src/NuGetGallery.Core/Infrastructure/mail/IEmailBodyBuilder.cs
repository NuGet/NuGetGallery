// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using NuGet.Services.Validation;

namespace NuGetGallery.Infrastructure.Mail
{
    public interface IEmailBodyBuilder
    {
        string ForReportAbuse(string galleryOwnerDisplayName, ReportPackageRequest request);
        string ForReportMyPackage(string galleryOwnerDisplayName, ReportPackageRequest request);
        string ForSendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl);
        string ForSendNewAccountEmail(User newUser, string confirmationUrl);
        string ForSendAccountDeleteNotice(User user);
        string ForSendContactSupportEmail(ContactSupportRequest request);
        string ForSendEmailChangeConfirmationNotice(string confirmationUrl, bool isOrganization);
        string ForSendEmailChangeNoticeToPreviousEmailAddress(bool isOrganization, User user, string oldEmailAddress);
        string ForSendOrganizationMemberRemovedNotice(Organization organization, User removedUser);
        string ForSendOrganizationMembershipRequest(User adminUser, bool isAdmin, Organization organization, string profileUrl, string confirmationUrl, string rejectionUrl);
        string ForSendOrganizationMembershipRequestCancelledNotice(Organization organization);
        string ForSendOrganizationMembershipRequestInitiatedNotice(User requestingUser, User pendingUser, Organization organization, bool isAdmin, string cancellationUrl);
        string ForSendOrganizationMembershipRequestRejectedNotice(User pendingUser);
        string ForSendOrganizationMemberUpdatedNotice(Membership membership, Organization organization);
        string ForSendOrganizationTransformInitiatedNotice(User accountToTransform, User adminUser, string cancellationUrl);
        string ForSendOrganizationTransformRequestAcceptedNotice(User accountToTransform, User adminUser);
        string ForSendOrganizationTransformRequest(User accountToTransform, string profileUrl, string confirmationUrl, string rejectionUrl);
        string ForSendPackageAddedNotice(bool hasWarnings, IEnumerable<string> warningMessages, Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
        string ForSendOrganizationTransformRequestRejectedNotice(User accountToTransform, User accountToReplyTo);
        string ForSendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl);
        string ForSendPackageOwnerAddedNotice(User newOwner, PackageRegistration package, string packageUrl);
        string ForSendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package);
        string ForSendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage);
        string ForSendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package);
        string ForSendSymbolPackageAddedNotice(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages, bool hasWarnings);
        string ForSendPackageOwnerRequestInitiatedNotice(User requestingOwner, User newOwner, PackageRegistration package, string cancellationUrl);
        string ForSendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package);
        string ForSendPasswordResetInstructions(string resetPasswordUrl, bool forgotPassword);
        string ForSendPackageAddedWithWarningsNotice(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages);
        string ForSendPackageValidationFailedNotice(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl);
        string ForSendSymbolPackageValidationFailedNotice(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl);
        string ForSendValidationTakingTooLongNotice(Package package, string packageUrl);
        string ForSendValidationTakingTooLongNotice(SymbolPackage symbolPackage, string packageUrl);
    }
}