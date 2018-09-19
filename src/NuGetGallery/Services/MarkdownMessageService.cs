// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Mail;
using NuGetGallery.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class MarkdownMessageService : CoreMarkdownMessageService, IMessageService
    {
        public MarkdownMessageService(
            IMailSender mailSender,
            IAppConfiguration config,
            ITelemetryService telemetryService)
            : base(mailSender, config)
        {
            this.telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            smtpUri = config.SmtpUri?.Host;
        }

        private readonly ITelemetryService telemetryService;
        private readonly string smtpUri;

        public IAppConfiguration Configuration
        {
            get { return (IAppConfiguration)CoreConfiguration; }
            set { CoreConfiguration = value; }
        }

        public async Task ReportAbuseAsync(ReportPackageRequest request)
        {
            var subject = EmailSubjectBuilder.ForReportAbuse(
                Configuration.GalleryOwner.DisplayName,
                request.Package.PackageRegistration.Id,
                request.Package.Version,
                request.Reason);

            var body = MarkdownEmailBodyBuilder.ForReportAbuse(
                Configuration.GalleryOwner.DisplayName,
                request);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Configuration.GalleryOwner);
                if (request.CopySender)
                {
                    // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                    // but here, the receiver is the gallery operators who already disclose their address
                    // CCing helps to create a thread of email that can be augmented by the sending user
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task ReportMyPackageAsync(ReportPackageRequest request)
        {
            var subject = EmailSubjectBuilder.ForReportMyPackage(
                Configuration.GalleryOwner.DisplayName,
                request.Package.PackageRegistration.Id,
                request.Package.Version,
                request.Reason);

            var body = MarkdownEmailBodyBuilder.ForReportMyPackage(
                Configuration.GalleryOwner.DisplayName,
                request);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Configuration.GalleryOwner);
                if (request.CopySender)
                {
                    // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                    // but here, the receiver is the gallery operators who already disclose their address
                    // CCing helps to create a thread of email that can be augmented by the sending user
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendContactOwnersMessageAsync(MailAddress fromAddress, Package package, string packageUrl, string htmlEncodedMessage, string emailSettingsUrl, bool copySender)
        {
            var subject = EmailSubjectBuilder.ForSendContactOwnersMessage(
                package.PackageRegistration.Id,
                Configuration.GalleryOwner.DisplayName);

            var body = MarkdownEmailBodyBuilder.ForSendContactOwnersMessage(
                fromAddress,
                package,
                packageUrl,
                htmlEncodedMessage,
                emailSettingsUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(fromAddress);

                AddOwnersToMailMessage(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                    if (copySender)
                    {
                        await SendMessageToSenderAsync(mailMessage);
                    }
                }
            }
        }

        public async Task SendNewAccountEmailAsync(User newUser, string confirmationUrl)
        {
            var subject = EmailSubjectBuilder.ForSendNewAccountEmail(Configuration.GalleryOwner.DisplayName);
            var body = MarkdownEmailBodyBuilder.ForSendNewAccountEmail(newUser, confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                mailMessage.To.Add(newUser.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendSigninAssistanceEmailAsync(MailAddress emailAddress, IEnumerable<Credential> credentials)
        {
            string body = @"Hi there,

We heard you were looking for Microsoft logins associated with your account on {0}. 

{1}

Thanks,

The {0} Team";

            string msaIdentity;
            if (credentials.Any())
            {
                var identities = string.Join("; ", credentials.Select(cred => cred.Identity).ToArray());
                msaIdentity = string.Format(@"Our records indicate the associated Microsoft login(s): {0}.", identities);
            }
            else
            {
                msaIdentity = "No associated Microsoft logins were found.";
            }

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Configuration.GalleryOwner.DisplayName,
                msaIdentity);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Sign-In Assistance.", Configuration.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                mailMessage.To.Add(emailAddress);
                await SendMessageAsync(mailMessage);
            }

        }

        public async Task SendEmailChangeConfirmationNoticeAsync(User user, string confirmationUrl)
        {
            var isOrganization = user is Organization;
            var subject = EmailSubjectBuilder.ForSendEmailChangeConfirmationNotice(Configuration.GalleryOwner.DisplayName, isOrganization);
            var body = MarkdownEmailBodyBuilder.ForSendEmailChangeConfirmationNotice(confirmationUrl, isOrganization);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.To.Add(new MailAddress(user.UnconfirmedEmailAddress, user.Username));

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendEmailChangeNoticeToPreviousEmailAddressAsync(User user, string oldEmailAddress)
        {
            var isOrganization = user is Organization;
            var subject = EmailSubjectBuilder.ForSendEmailChangeNoticeToPreviousEmailAddress(Configuration.GalleryOwner.DisplayName, isOrganization);
            var body = MarkdownEmailBodyBuilder.ForSendEmailChangeNoticeToPreviousEmailAddress(isOrganization, user, oldEmailAddress);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPasswordResetInstructionsAsync(User user, string resetPasswordUrl, bool forgotPassword)
        {
            var subject = EmailSubjectBuilder.ForSendPasswordResetInstructions(Configuration.GalleryOwner.DisplayName, forgotPassword);
            var body = MarkdownEmailBodyBuilder.ForSendPasswordResetInstructions(resetPasswordUrl, forgotPassword);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRequestAsync(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage)
        {
            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequest(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerRequest(fromUser, toUser, package, packageUrl, confirmationUrl, rejectionUrl, htmlEncodedMessage, policyMessage);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, toUser))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRequestInitiatedNoticeAsync(User requestingOwner, User receivingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestInitiatedNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerRequestInitiatedNotice(requestingOwner, newOwner, package, cancellationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(newOwner.ToMailAddress());

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, receivingOwner))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRequestRejectionNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            if (!requestingOwner.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestRejectionNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerRequestRejectionNotice(requestingOwner, newOwner, package);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(newOwner.ToMailAddress());

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, requestingOwner))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRequestCancellationNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestCancellationNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(requestingOwner.ToMailAddress());

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, newOwner))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerAddedNoticeAsync(User toUser, User newOwner, PackageRegistration package, string packageUrl)
        {
            var subject = EmailSubjectBuilder.ForSendPackageOwnerAddedNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerAddedNotice(newOwner, package, packageUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(Configuration.GalleryNoReplyAddress);

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, toUser))
                {
                    return;
                }
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRemovedNoticeAsync(User fromUser, User toUser, PackageRegistration package)
        {
            var subject = EmailSubjectBuilder.ForSendPackageOwnerRemovedNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageOwnerRemovedNotice(fromUser, toUser, package);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, toUser))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        private bool AddAddressesForPackageOwnershipManagementToEmail(MailMessage mailMessage, User user)
        {
            return AddAddressesWithPermissionToEmail(mailMessage, user, ActionsRequiringPermissions.HandlePackageOwnershipRequest);
        }

        public Task SendCredentialRemovedNoticeAsync(User user, CredentialViewModel removedCredentialViewModel)
        {
            if (CredentialTypes.IsApiKey(removedCredentialViewModel.Type))
            {
                return SendApiKeyChangeNoticeAsync(
                    user,
                    removedCredentialViewModel,
                    Strings.Emails_ApiKeyRemoved_Body,
                    Strings.Emails_CredentialRemoved_Subject);
            }
            else
            {
                return SendCredentialChangeNoticeAsync(
                    user,
                    removedCredentialViewModel,
                    Strings.Emails_CredentialRemoved_Body,
                    Strings.Emails_CredentialRemoved_Subject);
            }
        }

        public Task SendCredentialAddedNoticeAsync(User user, CredentialViewModel addedCredentialViewModel)
        {
            if (CredentialTypes.IsApiKey(addedCredentialViewModel.Type))
            {
                return SendApiKeyChangeNoticeAsync(
                    user,
                    addedCredentialViewModel,
                    Strings.Emails_ApiKeyAdded_Body,
                    Strings.Emails_CredentialAdded_Subject);
            }
            else
            {
                return SendCredentialChangeNoticeAsync(
                    user,
                    addedCredentialViewModel,
                    Strings.Emails_CredentialAdded_Body,
                    Strings.Emails_CredentialAdded_Subject);
            }
        }

        private Task SendApiKeyChangeNoticeAsync(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
        {
            string body = String.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                changedCredentialViewModel.Description);

            string subject = String.Format(
                CultureInfo.CurrentCulture,
                subjectTemplate,
                Configuration.GalleryOwner.DisplayName,
                Strings.CredentialType_ApiKey);

            return SendSupportMessageAsync(user, body, subject);
        }

        private Task SendCredentialChangeNoticeAsync(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
        {
            // What kind of credential is this?
            string name = changedCredentialViewModel.AuthUI == null ? changedCredentialViewModel.TypeCaption : changedCredentialViewModel.AuthUI.AccountNoun;

            string body = String.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                name);

            string subject = String.Format(
                CultureInfo.CurrentCulture,
                subjectTemplate,
                Configuration.GalleryOwner.DisplayName,
                name);

            return SendSupportMessageAsync(user, body, subject);
        }

        public async Task SendContactSupportEmailAsync(ContactSupportRequest request)
        {
            string subject = string.Format(CultureInfo.CurrentCulture, "Support Request (Reason: {0})", request.SubjectLine);

            string body = string.Format(CultureInfo.CurrentCulture, @"
**Email:** {0} ({1})

**Reason:**
{2}

**Message:**
{3}
", request.RequestingUser.Username, request.RequestingUser.EmailAddress, request.SubjectLine, request.Message);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Configuration.GalleryOwner);
                if (request.CopySender)
                {
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessageAsync(mailMessage);
            }
        }

        private async Task SendSupportMessageAsync(User user, string body, string subject)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryOwner;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendAccountDeleteNoticeAsync(User user)
        {
            string body = @"We received a request to delete your account {0}. If you did not initiate this request, please contact the {1} team immediately.
{2}When your account will be deleted, we will:{2}
 - revoke your API key(s)
 - remove you as the owner for any package you own 
 - remove your ownership from any ID prefix reservations and delete any ID prefix reservations that you were the only owner of 

{2}We will not delete the NuGet packages associated with the account.

Thanks,
{2}The {1} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                user.Username,
                Configuration.GalleryOwner.DisplayName,
                Environment.NewLine);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = CoreStrings.AccountDelete_SupportRequestTitle;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageDeletedNoticeAsync(Package package, string packageUrl, string packageSupportUrl)
        {
            var subject = EmailSubjectBuilder.ForSendPackageDeletedNotice(Configuration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageDeletedNotice(package, packageUrl, packageSupportUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;

                AddAllOwnersToMailMessage(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendOrganizationTransformRequestAsync(User accountToTransform, User adminUser, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            if (!adminUser.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequest(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationTransformRequest(accountToTransform, profileUrl, confirmationUrl, rejectionUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(accountToTransform.ToMailAddress());

                mailMessage.To.Add(adminUser.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationTransformInitiatedNoticeAsync(User accountToTransform, User adminUser, string cancellationUrl)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformInitiatedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationTransformInitiatedNotice(accountToTransform, adminUser, cancellationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(adminUser.ToMailAddress());

                mailMessage.To.Add(accountToTransform.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationTransformRequestAcceptedNoticeAsync(User accountToTransform, User adminUser)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequestAcceptedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationTransformRequestAcceptedNotice(accountToTransform, adminUser);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryOwner;
                mailMessage.ReplyToList.Add(adminUser.ToMailAddress());

                mailMessage.To.Add(accountToTransform.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public Task SendOrganizationTransformRequestRejectedNoticeAsync(User accountToTransform, User adminUser)
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

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequestRejectedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationTransformRequestRejectedNotice(accountToTransform, accountToReplyTo);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(accountToReplyTo.ToMailAddress());

                mailMessage.To.Add(accountToSendTo.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestAsync(Organization organization, User newUser, User adminUser, bool isAdmin, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            if (!newUser.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequest(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMembershipRequest(adminUser, isAdmin, organization, profileUrl, confirmationUrl, rejectionUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(organization.ToMailAddress());
                mailMessage.ReplyToList.Add(adminUser.ToMailAddress());

                mailMessage.To.Add(newUser.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestInitiatedNoticeAsync(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl)
        {
            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestInitiatedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMembershipRequestInitiatedNotice(requestingUser, pendingUser, organization, isAdmin, cancellationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(requestingUser.ToMailAddress());

                if (!AddAddressesForAccountManagementToEmail(mailMessage, organization))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestRejectedNoticeAsync(Organization organization, User pendingUser)
        {
            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestRejectedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMembershipRequestRejectedNotice(pendingUser);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(pendingUser.ToMailAddress());

                if (!AddAddressesForAccountManagementToEmail(mailMessage, organization))
                {
                    return;
                }

                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestCancelledNoticeAsync(Organization organization, User pendingUser)
        {
            if (!pendingUser.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestCancelledNotice(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMembershipRequestCancelledNotice(organization);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(organization.ToMailAddress());

                mailMessage.To.Add(pendingUser.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMemberUpdatedNoticeAsync(Organization organization, Membership membership)
        {
            if (!organization.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMemberUpdatedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMemberUpdatedNotice(membership, organization);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(membership.Member.ToMailAddress().Address);

                mailMessage.To.Add(organization.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMemberRemovedNoticeAsync(Organization organization, User removedUser)
        {
            if (!organization.EmailAllowed)
            {
                return;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMemberRemovedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var body = MarkdownEmailBodyBuilder.ForSendOrganizationMemberRemovedNotice(organization, removedUser);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Configuration.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(removedUser.ToMailAddress());

                mailMessage.To.Add(organization.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        private bool AddAddressesForAccountManagementToEmail(MailMessage mailMessage, User user)
        {
            return AddAddressesWithPermissionToEmail(mailMessage, user, ActionsRequiringPermissions.ManageAccount);
        }

        protected override async Task AttemptSendMessageAsync(MailMessage mailMessage, int attemptNumber)
        {
            bool success = false;
            DateTimeOffset startTime = DateTimeOffset.UtcNow;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await base.AttemptSendMessageAsync(mailMessage, attemptNumber);
                success = true;
            }
            finally
            {
                sw.Stop();
                telemetryService.TrackSendEmail(smtpUri, startTime, sw.Elapsed, success, attemptNumber);
            }
        }

        private bool AddAddressesWithPermissionToEmail(MailMessage mailMessage, User user, ActionRequiringAccountPermissions action)
        {
            if (user is Organization organization)
            {
                var membersAllowedToAct = organization.Members
                    .Where(m => action.CheckPermissions(m.Member, m.Organization) == PermissionsCheckResult.Allowed)
                    .Select(m => m.Member);

                bool hasRecipients = false;

                foreach (var member in membersAllowedToAct)
                {
                    if (!member.EmailAllowed)
                    {
                        continue;
                    }

                    mailMessage.To.Add(member.ToMailAddress());

                    hasRecipients = true;
                }

                return hasRecipients;
            }
            else
            {
                if (!user.EmailAllowed)
                {
                    return false;
                }

                mailMessage.To.Add(user.ToMailAddress());
                return true;
            }
        }
    }
}
