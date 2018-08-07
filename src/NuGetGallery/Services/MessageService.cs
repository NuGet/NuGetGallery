// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class MessageService : CoreMessageService, IMessageService
    {
        public MessageService(IMailSender mailSender, IAppConfiguration config, ITelemetryService telemetryService)
            : base(mailSender, config)
        {
            this.telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            smtpUri = config.SmtpUri?.Host;
        }

        private readonly ITelemetryService telemetryService;
        private readonly string smtpUri;

        public IAppConfiguration Config
        {
            get { return (IAppConfiguration)CoreConfiguration; }
            set { CoreConfiguration = value; }
        }

        public async Task ReportAbuseAsync(ReportPackageRequest request)
        {
            string subject = "[{GalleryOwnerName}] Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, Config);
            const string bodyTemplate = @"
**Email:** {Name} ({Address})

**Signature:** {Signature}

**Package:** {Id}
{PackageUrl}

**Version:** {Version}
{VersionUrl}
{User}

**Reason:**
{Reason}

**Has the package owner been contacted?:**
{AlreadyContactedOwners}

**Message:**
{Message}

";


            var body = new StringBuilder();
            body.Append(request.FillIn(bodyTemplate, Config));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = Config.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Config.GalleryOwner);
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
            string subject = "[{GalleryOwnerName}] Owner Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, Config);

            const string bodyTemplate = @"
**Email:** {Name} ({Address})

**Package:** {Id}
{PackageUrl}

**Version:** {Version}
{VersionUrl}
{User}

**Reason:**
{Reason}

**Message:**
{Message}

";

            var body = new StringBuilder();
            body.Append(request.FillIn(bodyTemplate, Config));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = Config.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Config.GalleryOwner);
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

        public async Task SendContactOwnersMessageAsync(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl, bool copySender)
        {
            string subject = "[{0}] Message for owners of the package '{1}'";
            string body = @"_User {0} &lt;{1}&gt; sends the following message to the owners of Package '[{2} {3}]({4})'._

{5}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    [change your email notification settings]({7}).
</em>";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                fromAddress.DisplayName,
                fromAddress.Address,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                message,
                Config.GalleryOwner.DisplayName,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, package.PackageRegistration.Id);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;
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
            var isOrganization = newUser is Organization;

            string body = $@"Thank you for {(isOrganization ? $"creating an organization on the" : $"registering with the")} {Config.GalleryOwner.DisplayName}.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address and click the following link:

[{HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_")}]({confirmationUrl})

Thanks,
The {Config.GalleryOwner.DisplayName} Team";

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Please verify your account", Config.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

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
                Config.GalleryOwner.DisplayName,
                msaIdentity);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Sign-In Assistance.", Config.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(emailAddress);
                await SendMessageAsync(mailMessage);
            }

        }
        
        public async Task SendEmailChangeConfirmationNoticeAsync(User user, string confirmationUrl)
        {
            string body = @"You recently changed your {0}'s {1} email address.

To verify {0} new email address:

[{2}]({3})

Thanks,
The {1} Team";

            var yourString = user is Organization ? "organization" : "account";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                yourString,
                Config.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);

            var newEmailAddress = new MailAddress(user.UnconfirmedEmailAddress, user.Username);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Please verify your {1}'s new email address", 
                    Config.GalleryOwner.DisplayName, yourString);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(newEmailAddress);
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendEmailChangeNoticeToPreviousEmailAddressAsync(User user, string oldEmailAddress)
        {
            string body = @"The email address associated with your {0} {1} was recently changed from _{2}_ to _{3}_.

Thanks,
The {0} Team";

            var yourString = user is Organization ? "organization" : "account";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                yourString,
                oldEmailAddress,
                user.EmailAddress);

            string subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Recent changes to your {1}'s email", Config.GalleryOwner.DisplayName, yourString);
            using (
                var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPasswordResetInstructionsAsync(User user, string resetPasswordUrl, bool forgotPassword)
        {
            string body = string.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? Strings.Emails_ForgotPassword_Body : Strings.Emails_SetPassword_Body,
                resetPasswordUrl,
                Config.GalleryOwner.DisplayName);

            string subject = string.Format(
                CultureInfo.CurrentCulture, forgotPassword ? Strings.Emails_ForgotPassword_Subject : Strings.Emails_SetPassword_Subject,
                Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRequestAsync(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string message, string policyMessage)
        {
            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = Environment.NewLine + policyMessage + Environment.NewLine;
            }

            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] Package ownership request for '{package.Id}'");

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' would like to add {(toUser is Organization ? "your organization" : "you")} as an owner of the package ['{package.Id}']({packageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(message))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' added the following message for you:

'{message}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(toUser is Organization ? "make your organization" : "become")} a listed owner of the package:

[{confirmationUrl}]({confirmationUrl})

To decline:

[{rejectionUrl}]({rejectionUrl})");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {Config.GalleryOwner.DisplayName} Team";

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] Package ownership request for '{package.Id}'");

            var body = string.Format(CultureInfo.CurrentCulture, $@"The user '{requestingOwner.Username}' has requested that user '{newOwner.Username}' be added as an owner of the package '{package.Id}'.

To cancel this request:

[{cancellationUrl}]({cancellationUrl})

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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

            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] Package ownership request for '{package.Id}' declined");

            var body = string.Format(CultureInfo.CurrentCulture, $@"The user '{newOwner.Username}' has declined {(requestingOwner is Organization ? "your organization's" : "your" )} request to add them as an owner of the package '{package.Id}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] Package ownership request for '{package.Id}' cancelled");

            var body = string.Format(CultureInfo.CurrentCulture, $@"The user '{requestingOwner.Username}' has cancelled their request for {(newOwner is Organization ? "your organization" : "you")} to be added as an owner of the package '{package.Id}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
            var subject = $"[{Config.GalleryOwner.DisplayName}] Package ownership update for '{package.Id}'";

            var body = $@"User '{newOwner.Username}' is now an owner of the package ['{package.Id}']({packageUrl}).

Thanks,
The {Config.GalleryOwner.DisplayName} Team";

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(Config.GalleryNoReplyAddress);

                if (!AddAddressesForPackageOwnershipManagementToEmail(mailMessage, toUser))
                {
                    return;
                }
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageOwnerRemovedNoticeAsync(User fromUser, User toUser, PackageRegistration package)
        {
            var subject = $"[{Config.GalleryOwner.DisplayName}] Package ownership removal for '{package.Id}'";

            var body = $@"The user '{fromUser.Username}' removed {(toUser is Organization ? "your organization" : "you")} as an owner of the package '{package.Id}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team";

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
                Config.GalleryOwner.DisplayName,
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
                Config.GalleryOwner.DisplayName,
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
                mailMessage.From = Config.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(Config.GalleryOwner);
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
                mailMessage.From = Config.GalleryOwner;

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
                Config.GalleryOwner.DisplayName,
                Environment.NewLine);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = Strings.AccountDelete_SupportRequestTitle;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageDeletedNoticeAsync(Package package, string packageUrl, string packageSupportUrl)
        {
            string subject = "[{0}] Package deleted - {1} {2}";
            string body = @"The package [{1} {2}]({3}) was just deleted from {0}. If this was not intended, please [contact support]({4}).

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                packageSupportUrl);

            subject = String.Format(
                CultureInfo.CurrentCulture,
                subject,
                Config.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Organization transformation for account '{accountToTransform.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"We have received a request to transform account ['{accountToTransform.Username}']({profileUrl}) into an organization.

To proceed with the transformation and become an administrator of '{accountToTransform.Username}':

[{confirmationUrl}]({confirmationUrl})

To cancel the transformation:

[{rejectionUrl}]({rejectionUrl})

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Organization transformation for account '{accountToTransform.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"We have received a request to transform account '{accountToTransform.Username}' into an organization with user '{adminUser.Username}' as its admin.

To cancel the transformation:

[{cancellationUrl}]({cancellationUrl})

If you did not request this change, please contact support by responding to this email.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;
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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Account '{accountToTransform.Username}' has been transformed into an organization";

            string body = string.Format(CultureInfo.CurrentCulture, $@"Account '{accountToTransform.Username}' has been transformed into an organization with user '{adminUser.Username}' as its administrator. If you did not request this change, please contact support by responding to this email.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;
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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Transformation of account '{accountToTransform.Username}' has been cancelled";

            string body = string.Format(CultureInfo.CurrentCulture, $@"Transformation of account '{accountToTransform.Username}' has been cancelled by user '{accountToReplyTo.Username}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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

            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";

            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership request for organization '{organization.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{adminUser.Username}' would like you to become {membershipLevel} of their organization, ['{organization.Username}']({profileUrl}).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become {membershipLevel} of '{organization.Username}':

[{confirmationUrl}]({confirmationUrl})

To decline the request:

[{rejectionUrl}]({rejectionUrl})

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(organization.ToMailAddress());
                mailMessage.ReplyToList.Add(adminUser.ToMailAddress());

                mailMessage.To.Add(newUser.ToMailAddress());
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendOrganizationMembershipRequestInitiatedNoticeAsync(Organization organization, User requestingUser, User pendingUser, bool isAdmin, string cancellationUrl)
        {
            var membershipLevel = isAdmin ? "an administrator" : "a collaborator";

            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership request for organization '{organization.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{requestingUser.Username}' has requested that user '{pendingUser.Username}' be added as {membershipLevel} of organization '{organization.Username}'. A confirmation mail has been sent to user '{pendingUser.Username}' to accept the membership request. This mail is to inform you of the membership changes to organization '{organization.Username}' and there is no action required from you.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' declined";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{pendingUser.Username}' has declined your request to become a member of your organization.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership request for organization '{organization.Username}' cancelled";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The request for you to become a member of '{organization.Username}' has been cancelled.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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

            var membershipLevel = membership.IsAdmin ? "an administrator" : "a collaborator";
            var member = membership.Member;

            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership update for organization '{organization.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{member.Username}' is now {membershipLevel} of organization '{organization.Username}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(member.ToMailAddress());

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

            string subject = $"[{Config.GalleryOwner.DisplayName}] Membership update for organization '{organization.Username}'";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{removedUser.Username}' is no longer a member of organization '{organization.Username}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
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
