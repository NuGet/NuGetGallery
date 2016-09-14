// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Services;
using System.Net.Mail;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IMessageService
    {
        Task SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl, bool copyFromAddress);
        Task ReportAbuse(ReportPackageRequest report);
        Task ReportMyPackage(ReportPackageRequest report);
        Task SendNewAccountEmail(MailAddress toAddress, string confirmationUrl);
        Task SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl);
        Task SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword);
        Task SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress);
        Task SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string confirmationUrl);
        Task SendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package);
        Task SendCredentialRemovedNotice(User user, Credential removed);
        Task SendCredentialAddedNotice(User user, Credential added);
        Task SendContactSupportEmail(ContactSupportRequest request);
        Task SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl);
    }
}