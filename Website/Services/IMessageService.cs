using System.Net.Mail;

namespace NuGetGallery {
    public interface IMessageService {
        MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message);
        MailMessage ReportAbuse(MailAddress fromAddress, Package package, string message);
        MailMessage SendNewAccountEmail(MailAddress toAddress, string confirmationUrl);
        MailMessage SendResetPasswordInstructions(MailAddress toAddress, string resetPasswordUrl);
    }
}