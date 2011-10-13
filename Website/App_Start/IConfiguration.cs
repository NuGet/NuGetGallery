using System.Net.Mail;

namespace NuGetGallery {
    public interface IConfiguration {
        string AzureStorageAccessKey { get; }
        string AzureStorageAccountName { get; }
        string AzureStorageBlobUrl { get; }
        bool ConfirmEmailAddresses { get; }
        MailAddress GalleryOwnerEmailAddress { get; }
        string PackageFileDirectory { get; }
        PackageStoreType PackageStoreType { get; }
        string SmtpHost { get; }
        string SmtpPassword { get; }
        int SmtpPort { get; }
        string SmtpUsername { get; }
        bool UseSmtp { get; }
    }
}