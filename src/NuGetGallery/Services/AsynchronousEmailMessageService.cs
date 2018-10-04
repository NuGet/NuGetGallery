// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging;
using NuGetGallery.Infrastructure.Mail;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public class AsynchronousEmailMessageService : CoreAsynchronousEmailMessageService, IMessageService
    {
        private readonly ITelemetryService _telemetryService;

        public AsynchronousEmailMessageService(
            IEmailMessageEnqueuer emailMessageEnqueuer,
            ITelemetryService telemetryService,
            ICoreMessageServiceConfiguration messageServiceConfiguration)
            : base(messageServiceConfiguration, emailMessageEnqueuer)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        public Task ReportAbuseAsync(ReportPackageRequest request)
        {
            var subject = EmailSubjectBuilder.ForReportAbuse(
                Configuration.GalleryOwner.DisplayName,
                request.Package.PackageRegistration.Id,
                request.Package.Version,
                request.Reason);

            var plainTextBody = PlainTextEmailBodyBuilder.ForReportAbuse(
                Configuration.GalleryOwner.DisplayName,
                request);

            var htmlBody = HtmlEmailBodyBuilder.ForReportAbuse(
                Configuration.GalleryOwner.DisplayName,
                request);

            var message = CreateMessage(
                request.FromAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { Configuration.GalleryOwner.Address },

                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                copySender: request.CopySender,
                discloseSenderAddress: true);

            return EnqueueMessageAsync(message);
        }

        public Task ReportMyPackageAsync(ReportPackageRequest request)
        {
            var subject = EmailSubjectBuilder.ForReportMyPackage(
                Configuration.GalleryOwner.DisplayName,
                request.Package.PackageRegistration.Id,
                request.Package.Version,
                request.Reason);

            var plainTextBody = PlainTextEmailBodyBuilder.ForReportMyPackage(
                Configuration.GalleryOwner.DisplayName,
                request);

            var htmlBody = HtmlEmailBodyBuilder.ForReportMyPackage(
                Configuration.GalleryOwner.DisplayName,
                request);

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { Configuration.GalleryOwner.Address },
                replyTo: new[] { request.FromAddress.Address },

                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                copySender: request.CopySender,
                discloseSenderAddress: true);

            return EnqueueMessageAsync(message);
        }

        public Task SendAccountDeleteNoticeAsync(User user)
        {
            var subject = EmailSubjectBuilder.ForSendAccountDeleteNotice();
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendAccountDeleteNotice(user);
            var htmlBody = HtmlEmailBodyBuilder.ForSendAccountDeleteNotice(user);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { user.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public async Task SendContactOwnersMessageAsync(MailAddress fromAddress, Package package, string packageUrl, string htmlEncodedMessage, string emailSettingsUrl, bool copySender)
        {
            var subject = EmailSubjectBuilder.ForSendContactOwnersMessage(package.PackageRegistration.Id, Configuration.GalleryOwner.DisplayName);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendContactOwnersMessage(fromAddress, package, packageUrl, htmlEncodedMessage, emailSettingsUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendContactOwnersMessage(fromAddress, package, packageUrl, htmlEncodedMessage, emailSettingsUrl);

            // Add owners to To list
            var to = new List<string>();
            foreach (var owner in package.PackageRegistration.Owners.Where(o => o.EmailAllowed))
            {
                to.Add(owner.ToMailAddress().Address);
            }

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { Configuration.GalleryOwner.Address },
                replyTo: new[] { fromAddress.Address },
                copySender: false /* We use a second email to copy the sender to avoid disclosing the receiver's address */);

            if (message.To.AnySafe())
            {
                await EnqueueMessageAsync(message);

                if (copySender)
                {
                    await SendMessageToSenderAsync(message);
                }
            }
        }

        public Task SendContactSupportEmailAsync(ContactSupportRequest request)
        {
            var subject = EmailSubjectBuilder.ForSendContactSupportEmail(request.SubjectLine);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendContactSupportEmail(request);
            var htmlBody = HtmlEmailBodyBuilder.ForSendContactSupportEmail(request);

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { Configuration.GalleryOwner.Address },
                replyTo: new[] { request.FromAddress.Address },

                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                copySender: request.CopySender,
                discloseSenderAddress: true);

            return EnqueueMessageAsync(message);
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

        public Task SendEmailChangeConfirmationNoticeAsync(User user, string confirmationUrl)
        {
            var isOrganization = user is Organization;
            var subject = EmailSubjectBuilder.ForSendEmailChangeConfirmationNotice(Configuration.GalleryOwner.DisplayName, isOrganization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendEmailChangeConfirmationNotice(confirmationUrl, isOrganization);
            var htmlBody = HtmlEmailBodyBuilder.ForSendEmailChangeConfirmationNotice(confirmationUrl, isOrganization);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { user.UnconfirmedEmailAddress });

            return EnqueueMessageAsync(message);
        }

        public Task SendEmailChangeNoticeToPreviousEmailAddressAsync(User user, string oldEmailAddress)
        {
            var isOrganization = user is Organization;
            var subject = EmailSubjectBuilder.ForSendEmailChangeNoticeToPreviousEmailAddress(Configuration.GalleryOwner.DisplayName, isOrganization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendEmailChangeNoticeToPreviousEmailAddress(isOrganization, user, oldEmailAddress);
            var htmlBody = HtmlEmailBodyBuilder.ForSendEmailChangeNoticeToPreviousEmailAddress(isOrganization, user, oldEmailAddress);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { oldEmailAddress });

            return EnqueueMessageAsync(message);
        }

        public Task SendNewAccountEmailAsync(User newUser, string confirmationUrl)
        {
            var subject = EmailSubjectBuilder.ForSendNewAccountEmail(Configuration.GalleryOwner.DisplayName);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendNewAccountEmail(newUser, confirmationUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendNewAccountEmail(newUser, confirmationUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { newUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMemberRemovedNoticeAsync(Organization organization, User removedUser)
        {
            if (!organization.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMemberRemovedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMemberRemovedNotice(organization, removedUser);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMemberRemovedNotice(organization, removedUser);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { organization.ToMailAddress().Address },
                replyTo: new[] { removedUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMembershipRequestAsync(Organization organization, User newUser, User adminUser, bool isAdmin, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            if (!newUser.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequest(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMembershipRequest(adminUser, isAdmin, organization, profileUrl, confirmationUrl, rejectionUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMembershipRequest(adminUser, isAdmin, organization, profileUrl, confirmationUrl, rejectionUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { newUser.ToMailAddress().Address },
                replyTo: new[]
                {
                    organization.ToMailAddress().Address,
                    adminUser.ToMailAddress().Address
                });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMembershipRequestCancelledNoticeAsync(Organization organization, User pendingUser)
        {
            if (!pendingUser.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestCancelledNotice(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMembershipRequestCancelledNotice(organization);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMembershipRequestCancelledNotice(organization);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { pendingUser.ToMailAddress().Address },
                replyTo: new[] { organization.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMembershipRequestInitiatedNoticeAsync(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl)
        {
            var to = AddAddressesForAccountManagementToRecipients(organization);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestInitiatedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMembershipRequestInitiatedNotice(requestingUser, pendingUser, organization, isAdmin, cancellationUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMembershipRequestInitiatedNotice(requestingUser, pendingUser, organization, isAdmin, cancellationUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { requestingUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMembershipRequestRejectedNoticeAsync(Organization organization, User pendingUser)
        {
            var to = AddAddressesForAccountManagementToRecipients(organization);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMembershipRequestRejectedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMembershipRequestRejectedNotice(pendingUser);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMembershipRequestRejectedNotice(pendingUser);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { pendingUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationMemberUpdatedNoticeAsync(Organization organization, Membership membership)
        {
            if (!organization.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationMemberUpdatedNotice(Configuration.GalleryOwner.DisplayName, organization);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationMemberUpdatedNotice(membership, organization);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationMemberUpdatedNotice(membership, organization);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { organization.ToMailAddress().Address },
                replyTo: new[] { membership.Member.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationTransformInitiatedNoticeAsync(User accountToTransform, User adminUser, string cancellationUrl)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformInitiatedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationTransformInitiatedNotice(accountToTransform, adminUser, cancellationUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationTransformInitiatedNotice(accountToTransform, adminUser, cancellationUrl);

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { accountToTransform.ToMailAddress().Address },
                replyTo: new[] { adminUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationTransformRequestAcceptedNoticeAsync(User accountToTransform, User adminUser)
        {
            if (!accountToTransform.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequestAcceptedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationTransformRequestAcceptedNotice(accountToTransform, adminUser);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationTransformRequestAcceptedNotice(accountToTransform, adminUser);

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { accountToTransform.ToMailAddress().Address },
                replyTo: new[] { adminUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationTransformRequestAsync(User accountToTransform, User adminUser, string profileUrl, string confirmationUrl, string rejectionUrl)
        {
            if (!adminUser.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequest(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationTransformRequest(accountToTransform, profileUrl, confirmationUrl, rejectionUrl);
            var htmlBody = PlainTextEmailBodyBuilder.ForSendOrganizationTransformRequest(accountToTransform, profileUrl, confirmationUrl, rejectionUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { adminUser.ToMailAddress().Address },
                replyTo: new[] { accountToTransform.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendOrganizationTransformRequestCancelledNoticeAsync(User accountToTransform, User adminUser)
        {
            return SendOrganizationTransformRequestRejectedNoticeInternalAsync(accountToTransform, adminUser, isCancelledByAdmin: false);
        }

        public Task SendOrganizationTransformRequestRejectedNoticeAsync(User accountToTransform, User adminUser)
        {
            return SendOrganizationTransformRequestRejectedNoticeInternalAsync(accountToTransform, adminUser, isCancelledByAdmin: true);
        }

        public Task SendPackageDeletedNoticeAsync(Package package, string packageUrl, string packageSupportUrl)
        {
            var to = AddAllOwnersToRecipients(package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageDeletedNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageDeletedNotice(package, packageUrl, packageSupportUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageDeletedNotice(package, packageUrl, packageSupportUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerAddedNoticeAsync(User toUser, User newOwner, PackageRegistration package, string packageUrl)
        {
            var to = AddAddressesForPackageOwnershipManagementToRecipients(toUser);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerAddedNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerAddedNotice(newOwner, package, packageUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerAddedNotice(newOwner, package, packageUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { Configuration.GalleryNoReplyAddress.Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerRemovedNoticeAsync(User fromUser, User toUser, PackageRegistration package)
        {
            var to = AddAddressesForPackageOwnershipManagementToRecipients(toUser);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRemovedNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerRemovedNotice(fromUser, toUser, package);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerRemovedNotice(fromUser, toUser, package);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { fromUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerRequestAsync(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string htmlEncodedMessage, string policyMessage)
        {
            var to = AddAddressesForPackageOwnershipManagementToRecipients(toUser);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequest(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerRequest(fromUser, toUser, package, packageUrl, confirmationUrl, rejectionUrl, htmlEncodedMessage, policyMessage);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerRequest(fromUser, toUser, package, packageUrl, confirmationUrl, rejectionUrl, htmlEncodedMessage, policyMessage);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { fromUser.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerRequestCancellationNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            var to = AddAddressesForPackageOwnershipManagementToRecipients(newOwner);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestCancellationNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerRequestCancellationNotice(requestingOwner, newOwner, package);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { requestingOwner.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerRequestInitiatedNoticeAsync(User requestingOwner, User receivingOwner, User newOwner, PackageRegistration package, string cancellationUrl)
        {
            var to = AddAddressesForPackageOwnershipManagementToRecipients(receivingOwner);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestInitiatedNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerRequestInitiatedNotice(requestingOwner, newOwner, package, cancellationUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerRequestInitiatedNotice(requestingOwner, newOwner, package, cancellationUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { newOwner.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageOwnerRequestRejectionNoticeAsync(User requestingOwner, User newOwner, PackageRegistration package)
        {
            if (!requestingOwner.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var to = AddAddressesForPackageOwnershipManagementToRecipients(requestingOwner);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageOwnerRequestRejectionNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageOwnerRequestRejectionNotice(requestingOwner, newOwner, package);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageOwnerRequestRejectionNotice(requestingOwner, newOwner, package);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to,
                replyTo: new[] { newOwner.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendPasswordResetInstructionsAsync(User user, string resetPasswordUrl, bool forgotPassword)
        {
            var subject = EmailSubjectBuilder.ForSendPasswordResetInstructions(Configuration.GalleryOwner.DisplayName, forgotPassword);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPasswordResetInstructions(resetPasswordUrl, forgotPassword);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPasswordResetInstructions(resetPasswordUrl, forgotPassword);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { user.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        public Task SendSigninAssistanceEmailAsync(MailAddress emailAddress, IEnumerable<Credential> credentials)
        {
            throw new System.NotImplementedException();
        }

        private static IReadOnlyList<string> AddAddressesForAccountManagementToRecipients(User user)
        {
            return AddAddressesWithPermissionToRecipients(user, ActionsRequiringPermissions.ManageAccount);
        }

        private static IReadOnlyList<string> AddAddressesWithPermissionToRecipients(User user, ActionRequiringAccountPermissions action)
        {
            var recipients = new List<string>();

            if (user is Organization organization)
            {
                var membersAllowedToAct = organization.Members
                    .Where(m => action.CheckPermissions(m.Member, m.Organization) == PermissionsCheckResult.Allowed)
                    .Select(m => m.Member);

                foreach (var member in membersAllowedToAct)
                {
                    if (!member.EmailAllowed)
                    {
                        continue;
                    }

                    recipients.Add(member.ToMailAddress().Address);
                }

                return recipients;
            }
            else
            {
                if (!user.EmailAllowed)
                {
                    return recipients;
                }

                recipients.Add(user.ToMailAddress().Address);
                return recipients;
            }
        }

        private static IReadOnlyList<string> AddAddressesForPackageOwnershipManagementToRecipients(User user)
        {
            return AddAddressesWithPermissionToRecipients(user, ActionsRequiringPermissions.HandlePackageOwnershipRequest);
        }

        private Task SendMessageToSenderAsync(EmailMessageData message)
        {
            var messageCopy = CreateMessage(
                message.Sender,
                message.Subject + " [Sender Copy]",
                string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        Configuration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        message.PlainTextBody),
                string.Format(
                        CultureInfo.CurrentCulture,
                        "<p>You sent the following message via {0}:</p><br/><br/>{1}",
                        Configuration.GalleryOwner.DisplayName,
                        message.PlainTextBody),
                to: new[] { message.ReplyTo.First() },
                cc: null,
                bcc: null,
                replyTo: new[] { message.ReplyTo.First() });

            return EnqueueMessageAsync(message);
        }

        private Task SendApiKeyChangeNoticeAsync(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
        {
            var body = string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                changedCredentialViewModel.Description);

            var subject = string.Format(
                CultureInfo.CurrentCulture,
                subjectTemplate,
                Configuration.GalleryOwner.DisplayName,
                Strings.CredentialType_ApiKey);

            return SendSupportMessageAsync(user, body, body, subject);
        }

        private Task SendCredentialChangeNoticeAsync(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
        {
            // What kind of credential is this?
            var name = changedCredentialViewModel.AuthUI == null ? changedCredentialViewModel.TypeCaption : changedCredentialViewModel.AuthUI.AccountNoun;

            var body = string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                name);

            var subject = string.Format(
                CultureInfo.CurrentCulture,
                subjectTemplate,
                Configuration.GalleryOwner.DisplayName,
                name);

            return SendSupportMessageAsync(user, body, body, subject);
        }

        private Task SendSupportMessageAsync(User user, string plainTextBody, string htmlBody, string subject)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var message = CreateMessage(
                Configuration.GalleryOwner.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { user.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }

        private Task SendOrganizationTransformRequestRejectedNoticeInternalAsync(User accountToTransform, User adminUser, bool isCancelledByAdmin)
        {
            var accountToSendTo = isCancelledByAdmin ? accountToTransform : adminUser;
            var accountToReplyTo = isCancelledByAdmin ? adminUser : accountToTransform;

            if (!accountToSendTo.EmailAllowed)
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendOrganizationTransformRequestRejectedNotice(Configuration.GalleryOwner.DisplayName, accountToTransform);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendOrganizationTransformRequestRejectedNotice(accountToTransform, accountToReplyTo);
            var htmlBody = HtmlEmailBodyBuilder.ForSendOrganizationTransformRequestRejectedNotice(accountToTransform, accountToReplyTo);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to: new[] { accountToSendTo.ToMailAddress().Address },
                replyTo: new[] { accountToReplyTo.ToMailAddress().Address });

            return EnqueueMessageAsync(message);
        }
    }
}
