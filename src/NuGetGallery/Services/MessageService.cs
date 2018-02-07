// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;
using NuGetGallery.Services;

namespace NuGetGallery
{
    public class MessageService : CoreMessageService, IMessageService
    {
        protected MessageService()
        {
        }

        public MessageService(IMailSender mailSender, IAppConfiguration config)
            : base(mailSender, config)
        {
        }

        public IAppConfiguration Config
        {
            get { return (IAppConfiguration)CoreConfiguration; }
            set { CoreConfiguration = value; }
        }

        public void ReportAbuse(ReportPackageRequest request)
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
                SendMessage(mailMessage);
            }
        }

        public void ReportMyPackage(ReportPackageRequest request)
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
                SendMessage(mailMessage);
            }
        }

        public void SendContactOwnersMessage(MailAddress fromAddress, Package package, string packageUrl, string message, string emailSettingsUrl, bool copySender)
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
                    SendMessage(mailMessage, copySender);
                }
            }
        }

        public void SendNewAccountEmail(MailAddress toAddress, string confirmationUrl)
        {
            string body = @"Thank you for registering with the {0}.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address and click the following link:

[{1}]({2})

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Please verify your account.", Config.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(toAddress);
                SendMessage(mailMessage);
            }
        }

        public void SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl)
        {
            string body = @"You recently changed your {0} email address.

To verify your new email address, please click the following link:

[{1}]({2})

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Please verify your new email address.", Config.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(newEmailAddress);
                SendMessage(mailMessage);
            }
        }

        public void SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress)
        {
            string body = @"Hi there,

The email address associated to your {0} account was recently
changed from _{1}_ to _{2}_.

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                oldEmailAddress,
                user.EmailAddress);

            string subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Recent changes to your account.", Config.GalleryOwner.DisplayName);
            using (
                var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                SendMessage(mailMessage);
            }
        }

        public void SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword)
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
                SendMessage(mailMessage);
            }
        }

        public void SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string packageUrl, string confirmationUrl, string rejectionUrl, string message, string policyMessage)
        {
            if (!toUser.EmailAllowed)
            {
                return;
            }

            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = Environment.NewLine + policyMessage + Environment.NewLine;
            }

            const string subject = "[{0}] The user '{1}' would like to add you as an owner of the package '{2}'.";

            string body = string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' wants to add you as an owner of the package '{package.Id}'.

Package URL on NuGet.org: [{packageUrl}]({packageUrl})
{policyMessage}
To accept this request and become a listed owner of the package, click the following URL:

[{confirmationUrl}]({confirmationUrl})

If you do not want to be listed as an owner of this package, click the following URL:

[{rejectionUrl}]({rejectionUrl})");

            if (!string.IsNullOrWhiteSpace(message))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{fromUser.Username}' added the following message for you:

'{message}'");
            }

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {Config.GalleryOwner.DisplayName} Team";

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendPackageOwnerRequestRejectionNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            if (!requestingOwner.EmailAllowed)
            {
                return;
            }

            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] The user '{newOwner.Username}' has rejected your request to add them as an owner of the package '{package.Id}'.");

            var body = string.Format(CultureInfo.CurrentCulture, $@"The user '{newOwner.Username}' has rejected your request to add them as an owner of the package '{package.Id}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(newOwner.ToMailAddress());

                mailMessage.To.Add(requestingOwner.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendPackageOwnerRequestCancellationNotice(User requestingOwner, User newOwner, PackageRegistration package)
        {
            if (!newOwner.EmailAllowed)
            {
                return;
            }

            var subject = string.Format(CultureInfo.CurrentCulture, $"[{Config.GalleryOwner.DisplayName}] The user '{requestingOwner.Username}' has cancelled their request for you to be added as an owner of the package '{package.Id}'.");

            var body = string.Format(CultureInfo.CurrentCulture, $@"The user '{requestingOwner.Username}' has cancelled their request for you to be added as an owner of the package '{package.Id}'.

Thanks,
The {Config.GalleryOwner.DisplayName} Team");

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(requestingOwner.ToMailAddress());

                mailMessage.To.Add(newOwner.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendPackageOwnerAddedNotice(User toUser, User newOwner, PackageRegistration package, string packageUrl, string policyMessage)
        {
            if (!toUser.EmailAllowed)
            {
                return;
            }

            if (!string.IsNullOrEmpty(policyMessage))
            {
                policyMessage = Environment.NewLine + policyMessage + Environment.NewLine;
            }

            const string subject = "[{0}] The user '{1}' is now an owner of the package '{2}'.";

            string body = @"This is to inform you that '{0}' is now an owner of the package

{1}
{2}
Thanks,
The {3} Team";
            body = String.Format(CultureInfo.CurrentCulture, body, newOwner.Username, packageUrl, policyMessage, Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, newOwner.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(Config.GalleryNoReplyAddress);

                mailMessage.To.Add(toUser.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package)
        {
            if (!toUser.EmailAllowed)
            {
                return;
            }

            const string subject = "[{0}] The user '{1}' has removed you as an owner of the package '{2}'.";

            string body = @"The user '{0}' removed you as an owner of the package '{1}'.

If this was done incorrectly, we'd recommend contacting '{0}' at '{2}'.

Thanks,
The {3} Team";
            body = String.Format(CultureInfo.CurrentCulture, body, fromUser.Username, package.Id, fromUser.EmailAddress, Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendCredentialRemovedNotice(User user, CredentialViewModel removedCredentialViewModel)
        {
            if (CredentialTypes.IsApiKey(removedCredentialViewModel.Type))
            {
                SendApiKeyChangeNotice(
                    user,
                    removedCredentialViewModel,
                    Strings.Emails_ApiKeyRemoved_Body,
                    Strings.Emails_CredentialRemoved_Subject);
            }
            else
            {
                SendCredentialChangeNotice(
                    user,
                    removedCredentialViewModel,
                    Strings.Emails_CredentialRemoved_Body,
                    Strings.Emails_CredentialRemoved_Subject);
            }
            
        }

        public void SendCredentialAddedNotice(User user, CredentialViewModel addedCrdentialViewModel)
        {
            if (CredentialTypes.IsApiKey(addedCrdentialViewModel.Type))
            {
                SendApiKeyChangeNotice(
                    user,
                    addedCrdentialViewModel,
                    Strings.Emails_ApiKeyAdded_Body,
                    Strings.Emails_CredentialAdded_Subject);
            }
            else
            {
                SendCredentialChangeNotice(
                    user,
                    addedCrdentialViewModel,
                    Strings.Emails_CredentialAdded_Body,
                    Strings.Emails_CredentialAdded_Subject);
            }
        }

        private void SendApiKeyChangeNotice(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
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

            SendSupportMessage(user, body, subject);
        }

        private void SendCredentialChangeNotice(User user, CredentialViewModel changedCredentialViewModel, string bodyTemplate, string subjectTemplate)
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

            SendSupportMessage(user, body, subject);
        }

        public void SendContactSupportEmail(ContactSupportRequest request)
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
                SendMessage(mailMessage);
            }
        }

        private void SendSupportMessage(User user, string body, string subject)
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
                SendMessage(mailMessage);
            }
        }

        public void SendPackageUploadedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            string subject = "[{0}] Package uploaded - {1} {2}";
            string body = @"The package [{1} {2}]({3}) was just uploaded to {0}. If this was not intended, please [contact support]({4}).

Note: This package has not been published yet. It will appear in search results and will be available for install/restore after both validation and indexing are complete. Package validation and indexing may take up to an hour.

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {0} and
    [change your email notification settings]({5}).
</em>";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Config.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl,
                packageSupportUrl,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, package.PackageRegistration.Id, package.Version);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    SendMessage(mailMessage);
                }
            }
        }

        public void SendAccountDeleteNotice(MailAddress mailAddress, string account)
        {
            string body = @"We received a request to delete your account {0}. If you did not initiate this request please contact the {1} team immediately.
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
                account,
                Config.GalleryOwner.DisplayName,
                Environment.NewLine);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = Strings.AccountDelete_SupportRequestTitle;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryNoReplyAddress;

                mailMessage.To.Add(mailAddress.Address);
                SendMessage(mailMessage);
            }
        }

        public void SendPackageDeletedNotice(Package package, string packageUrl, string packageSupportUrl)
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
                    SendMessage(mailMessage);
                }
            }
        }

        protected override void SendMessage(MailMessage mailMessage, bool copySender)
        {
            try
            {
                base.SendMessage(mailMessage, copySender);
            }
            catch (InvalidOperationException ex)
            {
                // Log but swallow the exception
                QuietLog.LogHandledException(ex);
            }
            catch (SmtpException ex)
            {
                // Log but swallow the exception
                QuietLog.LogHandledException(ex);
            }
        }
    }
}
