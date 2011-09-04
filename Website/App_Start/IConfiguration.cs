
using System.Net.Mail;
namespace NuGetGallery {
    public interface IConfiguration {
        string PackageFileDirectory { get; }
        MailAddress GalleryOwnerEmailAddress { get; }
    }
}