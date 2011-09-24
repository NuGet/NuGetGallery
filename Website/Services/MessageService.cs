using System;
using System.Linq;
using System.Net.Mail;
using System.Web;
using AnglicanGeek.MarkdownMailer;

namespace NuGetGallery {
    public class MessageService : IMessageService {
        readonly IMailSender mailSender;
        IConfiguration configuration;

        public MessageService(IMailSender mailSender, IConfiguration configuration) {
            this.mailSender = mailSender;
            this.configuration = configuration;
        }

        public MailMessage ReportAbuse(MailAddress fromAddress, Package package, string message) {
            string subject = "[{0}] Abuse Report for Package '{1}' Version '{2}'";
            string body = @"_User {0} ({1}) reports the package '{2}' version '{3}' as abusive. 
{0} left the following information in the report:_

{4}

_Message sent from {5}_
";
            body = String.Format(body,
                fromAddress.DisplayName,
                fromAddress.Address,
                package.PackageRegistration.Id,
                package.Version,
                message,
                configuration.GalleryOwnerEmailAddress.DisplayName);

            var mailMessage = new MailMessage {
                Subject = String.Format(subject, configuration.GalleryOwnerEmailAddress.DisplayName, package.PackageRegistration.Id, package.Version),
                Body = body,
                From = fromAddress,
            };
            mailMessage.To.Add(configuration.GalleryOwnerEmailAddress);
            mailSender.Send(mailMessage);
            return mailMessage;
        }

        public MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message) {
            string subject = "[{0}] Message for owners of the package '{1}'";
            string body = @"_User {0} ({1}) sends the following message to the owners of Package '{2}'._

{3}

-----------------------------------------------
_This email is sent from an automated service - please do not reply directly to this email._

<em style=""font-size: 0.8em;"">We take your privacy seriously. To opt-out of future emails from NuGet.org, send an email 
message with subject line __{4} Opt-out__ to [{5}](mailto:{5}?subject=NuGet.org%20Opt-out).</em>";

            body = String.Format(body,
                fromAddress.DisplayName,
                fromAddress.Address,
                packageRegistration.Id,
                message,
                configuration.GalleryOwnerEmailAddress.DisplayName,
                configuration.GalleryOwnerEmailAddress.Address);

            var mailMessage = new MailMessage {
                Subject = String.Format(subject, configuration.GalleryOwnerEmailAddress.DisplayName, packageRegistration.Id),
                Body = body,
                From = fromAddress,
            };

            AddOwnersToMailMessage(packageRegistration, mailMessage);
            if (mailMessage.To.Any()) {
                mailSender.Send(mailMessage);
            }
            return mailMessage;
        }

        private static void AddOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage) {
            foreach (var owner in packageRegistration.Owners.Where(o => o.EmailAllowed)) {
                mailMessage.To.Add(new MailAddress(owner.EmailAddress, owner.Username));
            }
        }


        public MailMessage SendNewAccountEmail(MailAddress toAddress, string confirmationUrl) {
            string body = @"Thank you for registering with the {0}. 
We can't wait to see what packages you'll upload.

To verify that you own this e-mail address, please click the following link:

[{1}]({2})

Thanks,
The {0} Team";

            body = String.Format(body,
                configuration.GalleryOwnerEmailAddress.DisplayName,
                HttpUtility.UrlDecode(confirmationUrl),
                confirmationUrl);

            var mailMessage = new MailMessage {
                Subject = String.Format("[{0}] Please verify your account.", configuration.GalleryOwnerEmailAddress.DisplayName),
                Body = body,
                From = configuration.GalleryOwnerEmailAddress,
            };
            mailMessage.To.Add(toAddress);
            mailSender.Send(mailMessage);
            return mailMessage;
        }


        public MailMessage SendResetPasswordInstructions(MailAddress toAddress, string resetPasswordUrl) {
            string body = @"The word on the street is you lost your password. Sorry to hear it!
If you haven't forgotten your password you can safely ignore this email. Your password has not been changed.

Click the following link within the next {0} hours to reset your password:

[{1}]({1})

Thanks,
The {2} Team";

            body = String.Format(body,
                Const.DefaultPasswordResetTokenExpirationHours,
                resetPasswordUrl,
                configuration.GalleryOwnerEmailAddress.DisplayName);

            var mailMessage = new MailMessage {
                Subject = String.Format("[{0}] Please reset your password.", configuration.GalleryOwnerEmailAddress.DisplayName),
                Body = body,
                From = configuration.GalleryOwnerEmailAddress,
            };
            mailMessage.To.Add(toAddress);
            mailSender.Send(mailMessage);
            return mailMessage;
        }
    }
}