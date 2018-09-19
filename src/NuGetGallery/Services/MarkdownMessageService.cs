// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Infrastructure.Mail.Requests;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class MarkdownMessageService : CoreMarkdownMessageService, IMessageService
    {
        private readonly ITelemetryService _telemetryService;
        private readonly string _smtpUri;

        public MarkdownMessageService(
            IMailSender mailSender,
            IAppConfiguration config,
            ITelemetryService telemetryService)
            : base(mailSender, config)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _smtpUri = config.SmtpUri?.Host;
        }

        public async Task ReportAbuseAsync(ReportPackageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var mailMessage = CreateMailMessage(new ReportAbuseMessage(Configuration, request)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task ReportMyPackageAsync(ReportPackageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var mailMessage = CreateMailMessage(new ReportMyPackageMessage(Configuration, request)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendContactOwnersMessageAsync(ContactOwnersRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var mailMessage = CreateMailMessage(new ContactOwnersMessage(Configuration, request)))
            {
                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);

                    if (request.CopySender)
                    {
                        await SendMessageToSenderAsync(mailMessage);
                    }
                }
            }
        }

        public async Task SendNewAccountEmailAsync(User newUser, string confirmationUrl)
        {
            using (var mailMessage = CreateMailMessage(new NewAccountMessage(Configuration, newUser, confirmationUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendSigninAssistanceEmailAsync(MailAddress emailAddress, IEnumerable<Credential> credentials)
        {
            using (var mailMessage = CreateMailMessage(new SigninAssistanceMessage(Configuration, emailAddress, credentials)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendEmailChangeConfirmationNoticeAsync(User user, string confirmationUrl)
        {
            using (var mailMessage = CreateMailMessage(new EmailChangeConfirmationMessage(Configuration, user, confirmationUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendEmailChangeNoticeToPreviousEmailAddressAsync(User user, string oldEmailAddress)
        {
            using (var mailMessage = CreateMailMessage(new EmailChangeNoticeToPreviousEmailAddressMessage(Configuration, user, oldEmailAddress)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPasswordResetInstructionsAsync(User user, string resetPasswordUrl, bool forgotPassword)
        {
            using (var mailMessage = CreateMailMessage(new PasswordResetInstructionsMessage(Configuration, user, resetPasswordUrl, forgotPassword)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnershipRequestAsync(PackageOwnershipRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var mailMessage = CreateMailMessage(new PackageOwnershipRequestMessage(Configuration, request)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnershipRequestInitiatedNoticeAsync(User requestingOwner, User receivingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            using (var mailMessage = CreateMailMessage(
                new PackageOwnershipRequestInitiatedMessage(Configuration, requestingOwner, receivingOwner, newOwner, package, cancellationUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnershipRequestDeclinedNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            if (!requestingOwner.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(new PackageOwnershipRequestDeclinedMessage(Configuration, requestingOwner, newOwner, package)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnershipRequestCanceledNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            using (var mailMessage = CreateMailMessage(new PackageOwnershipRequestCanceledMessage(Configuration, requestingOwner, newOwner, package)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerAddedNoticeAsync(User toUser, User newOwner, PackageRegistration package, string packageUrl)
        {
            using (var mailMessage = CreateMailMessage(new PackageOwnerAddedMessage(Configuration, toUser, newOwner, package, packageUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRemovedNoticeAsync(User fromUser, User toUser, PackageRegistration package)
        {
            using (var mailMessage = CreateMailMessage(new PackageOwnerRemovedMessage(Configuration, fromUser, toUser, package)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendCredentialRemovedNoticeAsync(User user, CredentialViewModel removedCredentialViewModel)
        {
            if (CredentialTypes.IsApiKey(removedCredentialViewModel.Type))
            {
                using (var mailMessage = CreateMailMessage(
                    new ApiKeyRemovedMessage(Configuration, user, removedCredentialViewModel.Description)))
                {
                    await SendMessageAsync(mailMessage);
                }
            }
            else
            {
                // What kind of credential is this?
                var credentialType = removedCredentialViewModel.AuthUI == null ? removedCredentialViewModel.TypeCaption : removedCredentialViewModel.AuthUI.AccountNoun;

                using (var mailMessage = CreateMailMessage(
                    new CredentialRemovedMessage(Configuration, user, removedCredentialViewModel.Description, credentialType)))
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendCredentialAddedNoticeAsync(User user, CredentialViewModel addedCredentialViewModel)
        {
            if (CredentialTypes.IsApiKey(addedCredentialViewModel.Type))
            {
                using (var mailMessage = CreateMailMessage(
                    new ApiKeyAddedMessage(Configuration, user, addedCredentialViewModel.Description)))
                {
                    await SendMessageAsync(mailMessage);
                }
            }
            else
            {
                // What kind of credential is this?
                var credentialType = addedCredentialViewModel.AuthUI == null ? addedCredentialViewModel.TypeCaption : addedCredentialViewModel.AuthUI.AccountNoun;

                using (var mailMessage = CreateMailMessage(new CredentialAddedMessage(Configuration, user, credentialType)))
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendContactSupportEmailAsync(ContactSupportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var mailMessage = CreateMailMessage(new ContactSupportMessage(Configuration, request)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendAccountDeleteNoticeAsync(User user)
        {
            using (var mailMessage = CreateMailMessage(new AccountDeleteNoticeMessage(Configuration, user)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageDeletedNoticeAsync(Package package, string packageUrl, string packageSupportUrl)
        {
            using (var mailMessage = CreateMailMessage(new PackageDeletedNoticeMessage(Configuration, package, packageUrl, packageSupportUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationTransformRequestAsync(User accountToTransform, User adminUser, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            if (!adminUser.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationTransformRequestMessage(Configuration, accountToTransform, adminUser, profileUrl, confirmationUrl, rejectionUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationTransformInitiatedNoticeAsync(User accountToTransform, User adminUser, string cancellationUrl)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationTransformInitiatedMessage(Configuration, accountToTransform, adminUser, cancellationUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationTransformRequestAcceptedNoticeAsync(User accountToTransform, User adminUser)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationTransformAcceptedMessage(Configuration, accountToTransform, adminUser)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public Task SendOrganizationTransformRequestDeclinedNoticeAsync(User accountToTransform, User adminUser)
        {
            return SendOrganizationTransformRequestRejectedNoticeInternalAsync(accountToTransform, adminUser, isCancelledByAdmin: true);
        }

        public Task SendOrganizationTransformRequestCancelledNoticeAsync(User accountToTransform, User adminUser)
        {
            return SendOrganizationTransformRequestRejectedNoticeInternalAsync(accountToTransform, adminUser, isCancelledByAdmin: false);
        }

        private async Task SendOrganizationTransformRequestRejectedNoticeInternalAsync(User accountToTransform, User adminUser, bool isCancelledByAdmin)
        {
            var accountToSendTo = isCancelledByAdmin ? accountToTransform : adminUser;
            var accountToReplyTo = isCancelledByAdmin ? adminUser : accountToTransform;

            if (!accountToSendTo.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationTransformCanceledMessage(Configuration, accountToTransform, accountToSendTo, accountToReplyTo)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestAsync(OrganizationMembershipRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!request.NewUser.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(new OrganizationMembershipRequestMessage(Configuration, request)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestInitiatedNoticeAsync(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl)
        {
            using (var mailMessage = CreateMailMessage(
                new OrganizationMembershipRequestInitiatedMessage(Configuration, organization, requestingUser, pendingUser, isAdmin, cancellationUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestDeclinedNoticeAsync(Organization organization, User pendingUser)
        {
            using (var mailMessage = CreateMailMessage(
                new OrganizationMembershipRequestDeclinedMessage(Configuration, organization, pendingUser)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestCanceledNoticeAsync(Organization organization, User pendingUser)
        {
            if (!pendingUser.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationMembershipRequestCanceledMessage(Configuration, organization, pendingUser)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMemberUpdatedNoticeAsync(Organization organization, Membership membership)
        {
            if (!organization.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationMemberUpdatedMessage(Configuration, organization, membership)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMemberRemovedNoticeAsync(Organization organization, User removedUser)
        {
            if (!organization.EmailAllowed)
            {
                return;
            }

            using (var mailMessage = CreateMailMessage(
                new OrganizationMemberRemovedMessage(Configuration, organization, removedUser)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        protected override async Task AttemptSendMessageAsync(MailMessage mailMessage, int attemptNumber)
        {
            var success = false;
            var startTime = DateTimeOffset.UtcNow;
            var sw = Stopwatch.StartNew();
            try
            {
                await base.AttemptSendMessageAsync(mailMessage, attemptNumber);
                success = true;
            }
            finally
            {
                sw.Stop();
                _telemetryService.TrackSendEmail(_smtpUri, startTime, sw.Elapsed, success, attemptNumber);
            }
        }
    }
}
