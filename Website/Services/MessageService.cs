using System;
using System.Linq;
using System.Net.Mail;
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
            string subject = "Abuse Report for Package '{0}' Version '{1}'";
            string body = @"_User {0} ({1}) reports the package '{2}' version '{3}' as abusive. 
{0} left the following information in the report:_

{4}

_Message sent from NuGet.org_
";
            var mailMessage = new MailMessage {
                Subject = String.Format(subject, package.PackageRegistration.Id, package.Version),
                Body = String.Format(body, fromAddress.DisplayName, fromAddress.Address, package.PackageRegistration.Id, package.Version, message),
                From = fromAddress,
            };
            mailMessage.To.Add(configuration.GalleryOwnerEmailAddress);
            mailSender.Send(mailMessage);
            return mailMessage;
        }

        public MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message) {
            string subject = "NuGet.org: Message for owners of the package '{0}'";
            string body = @"_User {0} ({1}) sends the following message to the owners of Package '{2}'._

{3}

-----------------------------------------------
_This email is sent from an automated service - please do not reply directly to this email._

<em style=""font-size: 0.8em;"">We take your privacy seriously. To opt-out of future emails from NuGet.org, send an email 
message with subject line __NuGet.org Opt-out__ to [{4}](mailto:{4}?subject=NuGet.org%20Opt-out).</em>";
            var mailMessage = new MailMessage {
                Subject = String.Format(subject, packageRegistration.Id),
                Body = String.Format(body, fromAddress.DisplayName, fromAddress.Address, packageRegistration.Id, message, configuration.GalleryOwnerEmailAddress.Address),
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
            string body = @"Thank you for registering with NuGet Gallery.

__Final Step__
To verify that you own this e-mail address, please click the following link:
[{0}]({0})

__Troubleshooting:__
If clicking on the link above does not work, try the following:

Select and copy the entire link.
Open a browser window and paste the link in the address bar.
Click on __Go__ or press the __Enter__ or __Return__ key.

If you continue to have access problems or want to report other issues, please [contact us](mailto:{1}).";
            var mailMessage = new MailMessage {
                Subject = "Please verify your NuGet.org account.",
                Body = String.Format(body, confirmationUrl, configuration.GalleryOwnerEmailAddress.Address),
                From = configuration.GalleryOwnerEmailAddress,
            };
            mailMessage.To.Add(toAddress);
            mailSender.Send(mailMessage);
            return mailMessage;
        }
    }
}