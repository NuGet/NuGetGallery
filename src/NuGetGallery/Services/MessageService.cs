// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Services;
using System.Threading.Tasks;
using System.Net;
using System.Web.Hosting;

namespace NuGetGallery
{
    public class MessageService : IMessageService
    {
        /// <summary>
        /// Constructor for tests to specify a custom MailSender
        /// </summary>
        /// <param name="mailSender">MailSender to use to send mail</param>
        protected MessageService(IMailSender mailSender)
        {
            MailSender = mailSender;
        }

        public MessageService(IGalleryConfigurationService configService, AuthenticationService authService)
        {
            ConfigService = configService;
            AuthService = authService;
        }

        public IMailSender MailSender { get; protected set; }
        public IGalleryConfigurationService ConfigService { get; protected set; }
        public AuthenticationService AuthService { get; protected set; }

        public async Task ReportAbuse(ReportPackageRequest request)
        {
            var appConfig = await ConfigService.GetCurrent();

            string subject = "[{GalleryOwnerName}] Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, appConfig.GalleryOwner.DisplayName);
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
            body.Append(request.FillIn(bodyTemplate, appConfig.GalleryOwner.DisplayName));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", appConfig.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = appConfig.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(appConfig.GalleryOwner);
                if (request.CopySender)
                {
                    // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                    // but here, the receiver is the gallery operators who already disclose their address
                    // CCing helps to create a thread of email that can be augmented by the sending user
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessage(mailMessage);
            }
        }

        public async Task ReportMyPackage(ReportPackageRequest request)
        {
            var appConfig = await ConfigService.GetCurrent();

            string subject = "[{GalleryOwnerName}] Owner Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, appConfig.GalleryOwner.DisplayName);

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
            body.Append(request.FillIn(bodyTemplate, appConfig.GalleryOwner.DisplayName));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", appConfig.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = appConfig.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(appConfig.GalleryOwner);
                if (request.CopySender)
                {
                    // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                    // but here, the receiver is the gallery operators who already disclose their address
                    // CCing helps to create a thread of email that can be augmented by the sending user
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessage(mailMessage);
            }
        }

        public async Task SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl, bool copySender)
        {
            var appConfig = await ConfigService.GetCurrent();

            string subject = "[{0}] Message for owners of the package '{1}'";
            string body = @"_User {0} &lt;{1}&gt; sends the following message to the owners of Package '{2}'._

{3}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {4} and
    [change your email notification settings]({5}).
</em>";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                fromAddress.DisplayName,
                fromAddress.Address,
                packageRegistration.Id,
                message,
                appConfig.GalleryOwner.DisplayName,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, appConfig.GalleryOwner.DisplayName, packageRegistration.Id);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryOwner;
                mailMessage.ReplyToList.Add(fromAddress);

                AddOwnersToMailMessage(packageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessage(mailMessage, copySender);
                }
            }
        }

        public async Task SendNewAccountEmail(MailAddress toAddress, string confirmationUrl)
        {
            var appConfig = await ConfigService.GetCurrent();

            string body = @"Thank you for registering with the {0}.
We can't wait to see what packages you'll upload.

So we can be sure to contact you, please verify your email address and click the following link:

[{1}]({2})

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                appConfig.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Please verify your account.", appConfig.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;

                mailMessage.To.Add(toAddress);
                await SendMessage(mailMessage);
            }
        }

        public async Task SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl)
        {
            var appConfig = await ConfigService.GetCurrent();

            string body = @"You recently changed your {0} email address.

To verify your new email address, please click the following link:

[{1}]({2})

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                appConfig.GalleryOwner.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl).Replace("_", "\\_"),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Please verify your new email address.", appConfig.GalleryOwner.DisplayName);
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;

                mailMessage.To.Add(newEmailAddress);
                await SendMessage(mailMessage);
            }
        }

        public async Task SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress)
        {
            var appConfig = await ConfigService.GetCurrent();

            string body = @"Hi there,

The email address associated to your {0} account was recently
changed from _{1}_ to _{2}_.

Thanks,
The {0} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                appConfig.GalleryOwner.DisplayName,
                oldEmailAddress,
                user.EmailAddress);

            string subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Recent changes to your account.", appConfig.GalleryOwner.DisplayName);
            using (
                var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                await SendMessage(mailMessage);
            }
        }

        public async Task SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword)
        {
            var appConfig = await ConfigService.GetCurrent();

            string body = String.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? Strings.Emails_ForgotPassword_Body : Strings.Emails_SetPassword_Body,
                Constants.DefaultPasswordResetTokenExpirationHours,
                resetPasswordUrl,
                appConfig.GalleryOwner.DisplayName);

            string subject = String.Format(CultureInfo.CurrentCulture, forgotPassword ? Strings.Emails_ForgotPassword_Subject : Strings.Emails_SetPassword_Subject, (await ConfigService.GetCurrent()).GalleryOwner.DisplayName);
            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessage(mailMessage);
            }
        }


        public async Task SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string confirmationUrl)
        {
            var appConfig = await ConfigService.GetCurrent();

            if (!toUser.EmailAllowed)
            {
                return;
            }

            const string subject = "[{0}] The user '{1}' wants to add you as an owner of the package '{2}'.";

            string body = @"The user '{0}' wants to add you as an owner of the package '{1}'.
If you do not want to be listed as an owner of this package, simply delete this email.

To accept this request and become a listed owner of the package, click the following URL:

[{2}]({2})

Thanks,
The {3} Team";

            body = String.Format(CultureInfo.CurrentCulture, body, fromUser.Username, package.Id, confirmationUrl, appConfig.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, appConfig.GalleryOwner.DisplayName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                await SendMessage(mailMessage);
            }
        }

        public async Task SendPackageOwnerRemovedNotice(User fromUser, User toUser, PackageRegistration package)
        {
            var appConfig = await ConfigService.GetCurrent();

            if (!toUser.EmailAllowed)
            {
                return;
            }

            const string subject = "[{0}] The user '{1}' has removed you as an owner of the package '{2}'.";

            string body = @"The user '{0}' removed you as an owner of the package '{1}'.

If this was done incorrectly, we'd recommend contacting '{0}' at '{2}'.

Thanks,
The {3} Team";
            body = String.Format(CultureInfo.CurrentCulture, body, fromUser.Username, package.Id, fromUser.EmailAddress, appConfig.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, appConfig.GalleryOwner.DisplayName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                await SendMessage(mailMessage);
            }
        }

        public async Task SendCredentialRemovedNotice(User user, Credential removed)
        {
            await SendCredentialChangeNotice(
                user,
                removed,
                Strings.Emails_CredentialRemoved_Body,
                Strings.Emails_CredentialRemoved_Subject);
        }

        public async Task SendCredentialAddedNotice(User user, Credential added)
        {
            await SendCredentialChangeNotice(
                user,
                added,
                Strings.Emails_CredentialAdded_Body,
                Strings.Emails_CredentialAdded_Subject);
        }

        private async Task SendCredentialChangeNotice(User user, Credential changed, string bodyTemplate, string subjectTemplate)
        {
            // What kind of credential is this?
            var credViewModel = AuthService.DescribeCredential(changed);
            string name = credViewModel.AuthUI == null ? credViewModel.TypeCaption : credViewModel.AuthUI.AccountNoun;

            string body = String.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                name);
            string subject = String.Format(
                CultureInfo.CurrentCulture,
                subjectTemplate,
                (await ConfigService.GetCurrent()).GalleryOwner.DisplayName,
                name);
            await SendSupportMessage(user, body, subject);
        }

        public async Task SendContactSupportEmail(ContactSupportRequest request)
        {
            var appConfig = await ConfigService.GetCurrent();

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
                mailMessage.From = appConfig.GalleryOwner;
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(appConfig.GalleryOwner);
                if (request.CopySender)
                {
                    mailMessage.CC.Add(request.FromAddress);
                }
                await SendMessage(mailMessage);
            }
        }

        private async Task SendSupportMessage(User user, string body, string subject)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = (await ConfigService.GetCurrent()).GalleryOwner;

                mailMessage.To.Add(user.ToMailAddress());
                await SendMessage(mailMessage);
            }
        }

        private static async Task<IMailSender> CreateMailSender(IGalleryConfigurationService configService)
        {
            var configSmtpUri = (await configService.GetCurrent()).SmtpUri;
            if (configSmtpUri != null && configSmtpUri.IsAbsoluteUri)
            {
                var smtpUri = new SmtpUri(configSmtpUri);

                var mailSenderConfiguration = new MailSenderConfiguration
                {
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Host = smtpUri.Host,
                    Port = smtpUri.Port,
                    EnableSsl = smtpUri.Secure
                };

                if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
                {
                    mailSenderConfiguration.UseDefaultCredentials = false;
                    mailSenderConfiguration.Credentials = new NetworkCredential(
                        smtpUri.UserName,
                        smtpUri.Password);
                }

                return new MailSender(mailSenderConfiguration);
            }
            else
            {
                var mailSenderConfiguration = new MailSenderConfiguration
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = HostingEnvironment.MapPath("~/App_Data/Mail")
                };

                return new MailSender(mailSenderConfiguration);
            }
        }

        private async Task SendMessage(MailMessage mailMessage, bool copySender = false)
        {
            var appConfig = await ConfigService.GetCurrent();

            try
            {
                if (MailSender == null)
                {
                    MailSender = await CreateMailSender(ConfigService);
                }

                MailSender.Send(mailMessage);
                if (copySender)
                {
                    var senderCopy = new MailMessage(
                        appConfig.GalleryOwner,
                        mailMessage.ReplyToList.First())
                        {
                            Subject = mailMessage.Subject + " [Sender Copy]",
                            Body = String.Format(
                                CultureInfo.CurrentCulture,
                                "You sent the following message via {0}: {1}{1}{2}",
                                appConfig.GalleryOwner.DisplayName,
                                Environment.NewLine,
                                mailMessage.Body),
                        };
                    senderCopy.ReplyToList.Add(mailMessage.ReplyToList.First());
                    MailSender.Send(senderCopy);
                }
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

        public async Task SendPackageAddedNotice(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl)
        {
            var appConfig = await ConfigService.GetCurrent();

            string subject = "[{0}] Package published - {1} {2}";
            string body = @"The package [{1} {2}]({3}) was just published on {0}. If this was not intended, please [contact support]({4}).

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {0} and
    [change your email notification settings]({5}).
</em>";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                appConfig.GalleryOwner.DisplayName, 
                package.PackageRegistration.Id, 
                package.Version,
                packageUrl,
                packageSupportUrl,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, appConfig.GalleryOwner.DisplayName, package.PackageRegistration.Id, package.Version);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = appConfig.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessage(mailMessage);
                }
            }
        }

        private static void AddOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.EmailAllowed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        private static void AddOwnersSubscribedToPackagePushedNotification(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }
    }
}
