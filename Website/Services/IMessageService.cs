using System;
using System.Net.Mail;
using System.Text;
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

        internal string FillIn(string subject, IConfiguration config)
        {
            // note, format blocks {xxx} are matched by ordinal-case-sensitive comparison
            var ret = new StringBuilder(subject);
            Action<string, string> substitute = (target, value) => ret.Replace(target, Escape(value));

            substitute("{GalleryOwnerName}", config.GalleryOwnerName);
            substitute("{Id}", Package.PackageRegistration.Id);
            substitute("{Version}", Package.Version);
            substitute("{Reason}", Reason);
            if (RequestingUser != null)
            {
                substitute("{Username}", RequestingUser.Username);
                substitute("{UserUrl}", Url.User(RequestingUser));
            }
            substitute("{Name}", FromAddress.DisplayName);
            substitute("{Address}", FromAddress.Address);
            substitute("{AlreadyContactedOwners}", AlreadyContactedOwners ? "Yes" : "No" );
            substitute("{PackageUrl}", Url.Package(Package.PackageRegistration));
            substitute("{VersionUrl}", Url.Package(Package));
            substitute("{Reason}", Reason);
            substitute("{Message}", Message);

            ret.Replace(@"\{\", "{");
            return ret.ToString();
        }

        private string Escape(string s)
        {
            return s.Replace("{", @"\{\");
        }
    }
}