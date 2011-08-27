using System.Net.Mail;

namespace NuGetGallery {
    public interface IMessageService {
        MailMessage SendContactOwnersMessage(MailAddress fromAddress, PackageRegistration packageRegistration, string message);
    }
}