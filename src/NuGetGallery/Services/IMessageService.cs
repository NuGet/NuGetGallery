// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public interface IMessageService
    {
        void SendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl, bool copyFromAddress);
        void ReportAbuse(ReportPackageRequest report);
        void ReportMyPackage(ReportPackageRequest report);
        void SendNewAccountEmail(User newUser, string confirmationUrl);
        void SendEmailChangeConfirmationNotice(User user, string confirmationUrl);
        void SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword);
        void SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress);
        void SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string message, string policyMessage);
        void SendPackageOwnerRequestInitiatedNotice(User requestingOwner, User receivingOwner, User newOwner, PackageRegistration package, string cancellationUrl);
        void SendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package);
        void SendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package);
        void SendPackageOwnerAddedNotice(User toUser, User newOwner, PackageRegistration package, string packageUrl);
        void SendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package);
        void SendCredentialRemovedNotice(User user, CredentialViewModel removedCredentialViewModel);
        void SendCredentialAddedNotice(User user, CredentialViewModel addedCredentialViewModel);
        void SendContactSupportEmail(ContactSupportRequest request);
        void SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null);
        void SendAccountDeleteNotice(User user);
        void SendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl);
        void SendSigninAssistanceEmail(MailAddress emailAddress, IEnumerable<Credential> credentials);
        void SendOrganizationTransformRequest(User accountToTransform, User adminUser, string profileUrl, string confirmationUrl, string rejectionUrl);
        void SendOrganizationTransformInitiatedNotice(User accountToTransform, User adminUser, string cancellationUrl);
        void SendOrganizationTransformRequestAcceptedNotice(User accountToTransform, User adminUser);
        void SendOrganizationTransformRequestRejectedNotice(User accountToTransform, User adminUser);
        void SendOrganizationTransformRequestCancelledNotice(User accountToTransform, User adminUser);
        void SendOrganizationMembershipRequest(Organization organization, User newUser, User adminUser, bool isAdmin, string profileUrl, string confirmationUrl, string rejectionUrl);
        void SendOrganizationMembershipRequestInitiatedNotice(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl);
        void SendOrganizationMembershipRequestRejectedNotice(Organization organization, User pendingUser);
        void SendOrganizationMembershipRequestCancelledNotice(Organization organization, User pendingUser);
        void SendOrganizationMemberUpdatedNotice(Organization organization, Membership membership);
        void SendOrganizationMemberRemovedNotice(Organization organization, User removedUser);
    }
}