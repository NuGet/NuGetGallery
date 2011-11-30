using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Ninject;
using Ninject.Modules;

namespace NuGetGallery
{
    public class ContainerBindings : NinjectModule
    {
        public override void Load()
        {
            IConfiguration configuration = new Configuration();
            Bind<IConfiguration>()
                .ToMethod(context => configuration);

            Lazy<GallerySetting> gallerySetting = new Lazy<GallerySetting>(() =>
            {
                using (var entitiesContext = new EntitiesContext())
                {
                    var settingsRepo = new EntityRepository<GallerySetting>(entitiesContext);
                    return settingsRepo.GetAll().FirstOrDefault();
                }
            });

            Bind<GallerySetting>().ToMethod(c => gallerySetting.Value);

            Bind<ISearchService>()
                .To<LuceneSearchService>()
                .InRequestScope();

            Bind<EntitiesContext>()
                .ToMethod(context => new EntitiesContext())
                .InRequestScope();

            Bind<IEntityRepository<User>>()
                .To<EntityRepository<User>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageRegistration>>()
                .To<EntityRepository<PackageRegistration>>()
                .InRequestScope();

            Bind<IEntityRepository<Package>>()
                .To<EntityRepository<Package>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageAuthor>>()
                .To<EntityRepository<PackageAuthor>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageDependency>>()
                .To<EntityRepository<PackageDependency>>()
                .InRequestScope();

            Bind<IEntityRepository<PackageStatistics>>()
                .To<EntityRepository<PackageStatistics>>()
                .InRequestScope();

            Bind<IUserService>()
                .To<UserService>()
                .InRequestScope();

            Bind<IPackageService>()
                .To<PackageService>()
                .InRequestScope();

            Bind<ICryptographyService>()
                .To<CryptographyService>()
                .InRequestScope();

            Bind<IFormsAuthenticationService>()
                .To<FormsAuthenticationService>()
                .InSingletonScope();

            Bind<IControllerFactory>()
                .To<NuGetControllerFactory>()
                .InRequestScope();

            Bind<IIndexingService>()
                .To<LuceneIndexingService>()
                .InRequestScope();

            Lazy<IMailSender> mailSenderThunk = new Lazy<IMailSender>(() =>
            {
                var settings = Kernel.Get<GallerySetting>();
                if (settings.UseSmtp)
                {
                    var mailSenderConfiguration = new MailSenderConfiguration()
                    {
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Host = settings.SmtpHost,
                        Port = settings.SmtpPort,
                        EnableSsl = true
                    };

                    if (!String.IsNullOrWhiteSpace(settings.SmtpUsername))
                    {
                        mailSenderConfiguration.UseDefaultCredentials = false;
                        mailSenderConfiguration.Credentials = new NetworkCredential(
                            settings.SmtpUsername,
                            settings.SmtpPassword);
                    }

                    return new MailSender(mailSenderConfiguration);
                }
                else
                {
                    var mailSenderConfiguration = new MailSenderConfiguration()
                    {
                        DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                        PickupDirectoryLocation = HostingEnvironment.MapPath("~/App_Data/Mail")
                    };

                    return new MailSender(mailSenderConfiguration);
                }
            });

            Bind<IMailSender>()
                .ToMethod(context => mailSenderThunk.Value);

            Bind<IMessageService>()
                .To<MessageService>();

            Bind<IPrincipal>().ToMethod(context => HttpContext.Current.User);

            switch (configuration.PackageStoreType)
            {
                case PackageStoreType.FileSystem:
                case PackageStoreType.NotSpecified:
                    Bind<IFileSystemService>()
                        .To<FileSystemService>()
                        .InSingletonScope();
                    Bind<IFileStorageService>()
                        .To<FileSystemFileStorageService>()
                        .InSingletonScope();
                    break;
                case PackageStoreType.AzureStorageBlob:
                    Bind<ICloudBlobClient>()
                        .ToMethod(context => new CloudBlobClientWrapper(new CloudBlobClient(
                            new Uri(configuration.AzureStorageBlobUrl, UriKind.Absolute),
                            new StorageCredentialsAccountAndKey(configuration.AzureStorageAccountName, configuration.AzureStorageAccessKey))))
                        .InSingletonScope();
                    Bind<IFileStorageService>()
                        .To<CloudBlobFileStorageService>()
                        .InSingletonScope();
                    break;
            }

            Bind<IPackageFileService>()
                .To<PackageFileService>();

            Bind<IEntityRepository<PackageOwnerRequest>>()
                .To<EntityRepository<PackageOwnerRequest>>()
                .InRequestScope();

            Bind<IUploadFileService>()
                .To<UploadFileService>();
        }
    }
}
