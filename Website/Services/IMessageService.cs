using System;
using System.Net.Mail;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IMessageService
    {
        void SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message, string emailSettingsUrl);
        void ReportAbuse(ReportPackageRequest report);
        void ReportMyPackage(ReportPackageRequest report);
        void SendNewAccountEmail(MailAddress toAddress, string confirmationUrl);
        void SendEmailChangeConfirmationNotice(MailAddress newEmailAddress, string confirmationUrl);
        void SendPasswordResetInstructions(User user, string resetPasswordUrl);
        void SendEmailChangeNoticeToPreviousEmailAddress(User user, string oldEmailAddress);
        void SendPackageOwnerRequest(User fromUser, User toUser, PackageRegistration package, string confirmationUrl);
    }

    public class ReportPackageRequest
    {
        public MailAddress FromAddress { get; set; }
        public User RequestingUser { get; set; }
        public Package Package { get; set; }
        public string Reason { get; set; }
        public string Message { get; set; }
        public bool AlreadyContactedOwners { get; set; }
        public UrlHelper Url { get; set; }
    }
}