using System.Net.Mail;

namespace NuGetGallery
{
    public interface IMessageService
    {
        MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl);
        MailMessage ReportAbuse(MailAddress fromAddress, Package package, string message);
        MailMessage SendNewAccountEmail(MailAddress toAddress, string confirmationUrl);
        MailMessage SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl);
        MailMessage SendPasswordResetInstructions(User user, string resetPasswordUrl);
        MailMessage SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress);
        MailMessage SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string confirmationUrl);
    }
}