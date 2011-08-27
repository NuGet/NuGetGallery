using System;
using System.Linq;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;

namespace NuGetGallery {
    public class MessageService : IMessageService {
        readonly IMailSender mailSender;

        public MessageService(IMailSender mailSender) {
            this.mailSender = mailSender;
        }

        public MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message) {
            string subject = "NuGet.org: Message for owners of the package '{0}'";
            string body = @"_User {0} ({1}) sends the following message to the owners of Package '{2}'._

{3}

-----------------------------------------------
_This email is sent from an automated service - please do not reply directly to this email._

<em style=""font-size: 0.8em;"">We take your privacy seriously. To opt-out of future emails from NuGet.org, send an email 
message with subject line __NuGet.org Opt-out__ to [nugetgallery@outercurve.org](mailto:nugetgallery@outercurve.org?subject=NuGet.org%20Opt-out).</em>";
            var mailMessage = new MailMessage {
                Subject = String.Format(subject, packageRegistration.Id),
                Body = String.Format(body, fromAddress.DisplayName, fromAddress.Address, packageRegistration.Id, message),
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
    }
}