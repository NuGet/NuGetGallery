// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services;
using System.Net.Mail;

namespace NuGetGallery
{
    public interface IMessageService
    {
        void SendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl, bool copyFromAddress);
        void ReportAbuse(ReportPackageRequest report);
        void ReportMyPackage(ReportPackageRequest report);
        void SendNewAccountEmail(MailAddress toAddress, string confirmationUrl);
        void SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl);
        void SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword);
        void SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress);
        void SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string message, string policyMessage);
        void SendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package);
        void SendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package);
        void SendPackageOwnerAddedNotice(User toUser, User newOwner, PackageRegistration package, string packageUrl, string policyMessage);
        void SendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package);
        void SendCredentialRemovedNotice(User user, CredentialViewModel removedCredentialViewModel);
        void SendCredentialAddedNotice(User user, CredentialViewModel addedCrdentialViewModel);
        void SendContactSupportEmail(ContactSupportRequest request);
        void SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
        void SendPackageUploadedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
        void SendAccountDeleteNotice(MailAddress mailAddress, string userName);
        void SendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl);
    }
}