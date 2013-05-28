using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using AnglicanGeek.MarkdownMailer;
using Elmah;

namespace NuGetGallery
{
    public class MessageService : IMessageService
    {
        private readonly IMailSender _mailSender;
        private readonly IConfiguration _config;

        public MessageService(IMailSender mailSender, IConfiguration config)
        {
            _mailSender = mailSender;
            _config = config;
        }

        public void ReportAbuse(ReportPackageRequest request)
        {
            string subject = "[{GalleryOwnerName}] Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, _config);

            const string bodyTemplateUnauthenticated = @"
**Email:** {Name} ({Address})

**Package:** {Id}
{PackageUrl}

**Version:** {Version}
{VersionUrl}

**Owners:**
{OwnerList}

**Reason:**
{Reason}

**Has the package owner been contacted?:**
{AlreadyContactedOwners}

**Message:**
{Message}
";

            const string bodyTemplateAuthenticated = @"
**Email:** {Name} ({Address})

**Package:** {Id}
{PackageUrl}

**Version:** {Version}
{VersionUrl}

**Owners:**
{OwnerList}

**User:** {Username} ({UserAddress})
{UserUrl}

**Reason:**
{Reason}

**Has the package owner been contacted?:**
{AlreadyContactedOwners}

**Message:**
{Message}
";

            string bodyTemplate = request.RequestingUser != null ?
                bodyTemplateAuthenticated :
                bodyTemplateUnauthenticated;

            var body = new StringBuilder("");
            body.Append(request.FillIn(bodyTemplate, _config));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", _config.GalleryOwnerName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(_config.GalleryOwnerEmail);
                SendMessage(mailMessage);
            }
        }

        public void ReportMyPackage(ReportPackageRequest request)
        {
            string subject = "[{GalleryOwnerName}] Owner Support Request for '{Id}' version {Version} (Reason: {Reason})";
            subject = request.FillIn(subject, _config);

            const string bodyTemplate = @"
**Email:** {Name} ({Address})

**Package:** {Id}
{PackageUrl}

**Version:** {Version}
{VersionUrl}

**Owners:**
{OwnerList}

**User:** {Username} ({UserAddress})
{UserUrl}

**Reason:**
{Reason}

**Message:**
{Message}
";

            var body = new StringBuilder();
            body.Append(request.FillIn(bodyTemplate, _config));
            body.AppendFormat(CultureInfo.InvariantCulture, @"

*Message sent from {0}*", _config.GalleryOwnerName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body.ToString();
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);
                mailMessage.ReplyToList.Add(request.FromAddress);
                mailMessage.To.Add(_config.GalleryOwnerEmail);
                SendMessage(mailMessage);
            }
        }

        public void SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl)
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
                _config.GalleryOwnerName,
                emailSettingsUrl);

            subject = String.Format(CultureInfo.CurrentCulture, subject, _config.GalleryOwnerName, packageRegistration.Id);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);
                mailMessage.ReplyToList.Add(fromAddress);

                AddOwnersToMailMessage(packageRegistration, mailMessage);
                if (mailMessage.To.Any())
                {
                    SendMessage(mailMessage);
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
                _config.GalleryOwnerName,
                HttpUtility.UrlDecode(confirmationUrl),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Please verify your account.", _config.GalleryOwnerName);
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);

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
                _config.GalleryOwnerName,
                HttpUtility.UrlDecode(confirmationUrl),
                confirmationUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(
                    CultureInfo.CurrentCulture, "[{0}] Please verify your new email address.", _config.GalleryOwnerName);
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);

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
                _config.GalleryOwnerName,
                oldEmailAddress,
                user.EmailAddress);

            string subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Recent changes to your account.", _config.GalleryOwnerName);
            using (
                var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);

                mailMessage.To.Add(new MailAddress(oldEmailAddress, user.Username));
                SendMessage(mailMessage);
            }
        }

        public void SendPasswordResetInstructions(User user, string resetPasswordUrl)
        {
            string body = @"The word on the street is you lost your password. Sorry to hear it!
If you haven't forgotten your password you can safely ignore this email. Your password has not been changed.

Click the following link within the next {0} hours to reset your password:

[{1}]({1})

Thanks,
The {2} Team";

            body = String.Format(
                CultureInfo.CurrentCulture,
                body,
                Constants.DefaultPasswordResetTokenExpirationHours,
                resetPasswordUrl,
                _config.GalleryOwnerName);

            string subject = String.Format(CultureInfo.CurrentCulture, "[{0}] Please reset your password.", _config.GalleryOwnerName);
            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);

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

            body = String.Format(CultureInfo.CurrentCulture, body, fromUser.Username, package.Id, confirmationUrl, _config.GalleryOwnerName);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = String.Format(CultureInfo.CurrentCulture, subject, _config.GalleryOwnerName, fromUser.Username, package.Id);
                mailMessage.Body = body;
                mailMessage.From = new MailAddress(_config.GalleryOwnerEmail, _config.GalleryOwnerName);
                mailMessage.ReplyToList.Add(fromUser.ToMailAddress());

                mailMessage.To.Add(toUser.ToMailAddress());
                SendMessage(mailMessage);
            }
        }

        private void SendMessage(MailMessage mailMessage)
        {
            try
            {
                _mailSender.Send(mailMessage);
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