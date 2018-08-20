// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public interface IMessageService
    {
        Task SendContactOwnersMessageAsync(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl, bool copyFromAddress);
        Task ReportAbuseAsync(ReportPackageRequest report);
        Task ReportMyPackageAsync(ReportPackageRequest report);
        Task SendNewAccountEmailAsync(User newUser, string confirmationUrl);
        Task SendEmailChangeConfirmationNoticeAsync(User user, string confirmationUrl);
        Task SendPasswordResetInstructionsAsync(User user, string resetPasswordUrl, bool forgotPassword);
        Task SendEmailChangeNoticeToPreviousEmailAddressAsync(User user, string oldEmailAddress);
        Task SendPackageOwnerRequestAsync(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string message, string policyMessage);
        Task SendPackageOwnerRequestInitiatedNoticeAsync(User requestingOwner, User receivingOwner, User newOwner, PackageRegistration package, string cancellationUrl);
        Task SendPackageOwnerRequestRejectionNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package);
        Task SendPackageOwnerRequestCancellationNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package);
        Task SendPackageOwnerAddedNoticeAsync(User toUser, User newOwner, PackageRegistration package, string packageUrl);
        Task SendPackageOwnerRemovedNoticeAsync(User fromUser, User toUser, PackageRegistration package);
        Task SendCredentialRemovedNoticeAsync(User user, CredentialViewModel removedCredentialViewModel);
        Task SendCredentialAddedNoticeAsync(User user, CredentialViewModel addedCredentialViewModel);
        Task SendContactSupportEmailAsync(ContactSupportRequest request);
        Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null);
        Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages);
        Task SendAccountDeleteNoticeAsync(User user);
        Task SendPackageDeletedNoticeAsync(Package package, string packageUrl, string packageSupportUrl);
        Task SendSigninAssistanceEmailAsync(MailAddress emailAddress, IEnumerable<Credential> credentials);
        Task SendOrganizationTransformRequestAsync(User accountToTransform, User adminUser, string profileUrl, string confirmationUrl, string rejectionUrl);
        Task SendOrganizationTransformInitiatedNoticeAsync(User accountToTransform, User adminUser, string cancellationUrl);
        Task SendOrganizationTransformRequestAcceptedNoticeAsync(User accountToTransform, User adminUser);
        Task SendOrganizationTransformRequestRejectedNoticeAsync(User accountToTransform, User adminUser);
        Task SendOrganizationTransformRequestCancelledNoticeAsync(User accountToTransform, User adminUser);
        Task SendOrganizationMembershipRequestAsync(Organization organization, User newUser, User adminUser, bool isAdmin, string profileUrl, string confirmationUrl, string rejectionUrl);
        Task SendOrganizationMembershipRequestInitiatedNoticeAsync(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl);
        Task SendOrganizationMembershipRequestRejectedNoticeAsync(Organization organization, User pendingUser);
        Task SendOrganizationMembershipRequestCancelledNoticeAsync(Organization organization, User pendingUser);
        Task SendOrganizationMemberUpdatedNoticeAsync(Organization organization, Membership membership);
        Task SendOrganizationMemberRemovedNoticeAsync(Organization organization, User removedUser);
    }
}