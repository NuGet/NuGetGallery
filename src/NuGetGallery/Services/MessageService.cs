using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using AnglicanGeek.MarkdownMailer;
using Elmah;
using NuGetGallery.Authentication;
using Glimpse.AspNet.AlternateType;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class MessageService : IMessageService
    {
        public IMailSender MailSender { get; protected set; }
        public IAppConfiguration Config { get; protected set; }
        public AuthenticationService AuthService { get; protected set; }

        protected MessageService() { }

        public MessageService(IMailSender mailSender, IAppConfiguration config, AuthenticationService authService) :this()
        {
            MailSender = mailSender;
            Config = config;
            AuthService = authService;
        }

        public void ReportAbuse(ReportPackageRequest request)
        {
            string subject = "[{GalleryOwnerName}] Support Request for '{Id}' version {Version} (Reason: {Reason})";
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

**Has the package owner been contacted?:**
{AlreadyContactedOwners}

**Message:**
{Message}
";


            var body = new StringBuilder("");
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

        public void SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl, bool copySender)
        {
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
                Config.GalleryOwner.DisplayName,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, packageRegistration.Id);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;
                mailMessage.ReplyToList.Add(fromAddress);

                AddOwnersToMailMessage(packageRegistration, mailMessage);

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
                mailMessage.From = Config.GalleryOwner;

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
                mailMessage.From = Config.GalleryOwner;

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
                mailMessage.From = Config.GalleryOwner;

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                SendMessage(mailMessage);
            }
        }

        public void SendPasswordResetInstructions(User user, string resetPasswordUrl, bool forgotPassword)
        {
            string body = String.Format(
                CultureInfo.CurrentCulture,
                forgotPassword ? Strings.Emails_ForgotPassword_Body : Strings.Emails_SetPassword_Body,
                Constants.DefaultPasswordResetTokenExpirationHours,
                resetPasswordUrl,
                Config.GalleryOwner.DisplayName);

            string subject = String.Format(CultureInfo.CurrentCulture, forgotPassword ? Strings.Emails_ForgotPassword_Subject : Strings.Emails_SetPassword_Subject, Config.GalleryOwner.DisplayName);
            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;

                mailMessage.To.Add(user.ToMailAddress());
                SendMessage(mailMessage);
            }
        }


        public void SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string confirmationUrl)
        {
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

            body = String.Format(CultureInfo.CurrentCulture, body, fromUser.Username, package.Id, confirmationUrl, Config.GalleryOwner.DisplayName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, Config.GalleryOwner.DisplayName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        public void SendCredentialRemovedNotice(User user, Credential removed)
        {
            SendCredentialChangeNotice(
                user, 
                removed, 
                Strings.Emails_CredentialRemoved_Body, 
                Strings.Emails_CredentialRemoved_Subject);
        }

        public void SendCredentialAddedNotice(User user, Credential added)
        {
            SendCredentialChangeNotice(
                user,
                added,
                Strings.Emails_CredentialAdded_Body,
                Strings.Emails_CredentialAdded_Subject);
        }

        private void SendCredentialChangeNotice(User user, Credential changed, string bodyTemplate, string subjectTemplate)
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
                Config.GalleryOwner.DisplayName,
                name);
            SendSupportMessage(user, body, subject);
        }

        private void SendSupportMessage(User user, string body, string subject)
        {
            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = Config.GalleryOwner;

                mailMessage.To.Add(user.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        private void SendMessage(MailMessage mailMessage, bool copySender = false)
        {
            try
            {
                MailSender.Send(mailMessage);
                if (copySender)
                {
                    var senderCopy = new MailMessage(
                        Config.GalleryOwner,
                        mailMessage.ReplyToList.First()) {
                        Subject = mailMessage.Subject + " [Sender Copy]",
                        Body = String.Format(
                            CultureInfo.CurrentCulture,
                            "You sent the following message via {0}: {1}{1}{2}",
                            Config.GalleryOwner.DisplayName, 
                            Environment.NewLine, 
                            mailMessage.Body),
                    };
                    senderCopy.ReplyToList.Add(mailMessage.ReplyToList.First());
                    MailSender.Send(senderCopy);
                }
            }
            catch (SmtpException ex)
            {
                // Log but swallow the exception
                ErrorSignal.FromCurrentContext().Raise(ex);
            }
        }

        private static void AddOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.EmailAllowed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }
    }
}