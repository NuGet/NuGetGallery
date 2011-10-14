using System;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
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

            Lazy<IMailSender> mailSenderThunk = null;
            if (configuration.UseSmtp)
            {
                mailSenderThunk = new Lazy<IMailSender>(() =>
                {
                    var mailSenderConfiguration = new MailSenderConfiguration()
                    {
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Host = configuration.SmtpHost,
                        Port = configuration.SmtpPort,
                        EnableSsl = true,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(
                            configuration.SmtpUsername,
                            configuration.SmtpPassword)
                    };

                    return new MailSender(mailSenderConfiguration);
                });
            }
            else
            {
                mailSenderThunk = new Lazy<IMailSender>(() =>
                {
                    var mailSenderConfiguration = new MailSenderConfiguration()
                    {
                        DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                        PickupDirectoryLocation = HostingEnvironment.MapPath("~/App_Data/Mail")
                    };

                    return new MailSender(mailSenderConfiguration);
                });
            }
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
                        .InRequestScope();
                    Bind<IPackageFileService>()
                        .To<FileSystemPackageFileService>()
                        .InRequestScope();
                    break;
                case PackageStoreType.AzureStorageBlob:
                    Bind<ICloudBlobClient>()
                        .ToMethod(context => new CloudBlobClientWrapper(new CloudBlobClient(
                            new Uri(configuration.AzureStorageBlobUrl, UriKind.Absolute),
                            new StorageCredentialsAccountAndKey(configuration.AzureStorageAccountName, configuration.AzureStorageAccessKey))))
                        .InSingletonScope();
                    Bind<IPackageFileService>()
                        .To<CloudBlobPackageFileService>()
                        .InSingletonScope();
                    break;
            }
        }
    }
}
