using System.Net.Mail;

namespace NuGetGallery {
    public interface IConfiguration {
        bool ConfirmEmailAddresses { get; }
        string PackageFileDirectory { get; }
        MailAddress GalleryOwnerEmailAddress { get; }
    }
}