using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Microsoft.ApplicationServer.Caching;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using Ninject;
using Ninject.Modules;

namespace NuGetGallery
{
    public class ContainerBindings : NinjectModule
    {
        Func<IConfiguration, AzureCache> getAzureCache = new Func<IConfiguration, AzureCache>(configuration =>
        {
            var servers = new DataCacheServerEndpoint[1];
            servers[0] = new DataCacheServerEndpoint(configuration.AzureCacheServiceUrl, 22233);
            var authenticationToken = new SecureString();
            foreach (char @char in configuration.AzureCacheAuthenticationToken)
                authenticationToken.AppendChar(@char);
            authenticationToken.MakeReadOnly();
            DataCacheSecurity factorySecurity = new DataCacheSecurity(authenticationToken);
            DataCacheFactoryConfiguration factoryConfig = new DataCacheFactoryConfiguration();
            factoryConfig.Servers = servers;
            factoryConfig.SecurityProperties = factorySecurity;
            DataCacheFactory cacheFactory = new DataCacheFactory(factoryConfig);
            return new AzureCache(cacheFactory.GetDefaultCache());
        });

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

            Bind<ITaskFactory>()
                .ToMethod(context => new TaskFactoryWrapper(Task.Factory));

            Bind<ISearchService>()
                .To<LuceneSearchService>()
                .InRequestScope();

            Bind<IEntitiesContext>()
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

            Bind<INuGetExeDownloaderService>()
                .To<NuGetExeDownloaderService>()
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
                    Bind<IFileStorageService>()
                        .To<FileSystemFileStorageService>()
                        .InSingletonScope();
                    Bind<ICache>()
                        .To<LocalCache>()
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
                    Bind<ICache>()
                        .ToMethod(context => getAzureCache(configuration))
                        .InSingletonScope();
                    break;
            }

            Bind<IFileSystemService>()
                .To<FileSystemService>()
                .InSingletonScope();

            Bind<IPackageFileService>()
                .To<PackageFileService>();

            Bind<IEntityRepository<PackageOwnerRequest>>()
                .To<EntityRepository<PackageOwnerRequest>>()
                .InRequestScope();

            Bind<IUploadFileService>()
                .To<UploadFileService>();

            Bind<IAggregateStatsService>()
                .To<AggregateStatsService>()
                .InRequestScope();

            // todo: bind all package curators by convention
            Bind<IAutomaticPackageCurator>()
                .To<WebMatrixPackageCurator>();

            // todo: bind all commands by convention
            Bind<IAutomaticallyCuratePackageCommand>()
                .To<AutomaticallyCuratePackageCommand>()
                .InRequestScope();
            Bind<ICreateCuratedPackageCommand>()
                .To<CreateCuratedPackageCommand>()
                .InRequestScope();
            Bind<IDeleteCuratedPackageCommand>()
                .To<DeleteCuratedPackageCommand>()
                .InRequestScope();
            Bind<IModifyCuratedPackageCommand>()
                .To<ModifyCuratedPackageCommand>()
                .InRequestScope();

            // todo: bind all queries by convention
            Bind<ICuratedFeedByKeyQuery>()
                .To<CuratedFeedByKeyQuery>()
                .InRequestScope();
            Bind<ICuratedFeedByNameQuery>()
                .To<CuratedFeedByNameQuery>()
                .InRequestScope();
            Bind<ICuratedFeedsByManagerQuery>()
                .To<CuratedFeedsByManagerQuery>()
                .InRequestScope();
            Bind<IPackageRegistrationByKeyQuery>()
                .To<PackageRegistrationByKeyQuery>()
                .InRequestScope();
            Bind<IPackageRegistrationByIdQuery>()
                .To<PackageRegistrationByIdQuery>()
                .InRequestScope();
            Bind<IUserByUsernameQuery>()
                .To<UserByUsernameQuery>()
                .InRequestScope();
            Bind<IPackageIdsQuery>()
                .To<PackageIdsQuery>()
                .InRequestScope();
            Bind<IPackageVersionsQuery>()
                .To<PackageVersionsQuery>()
                .InRequestScope();

            Bind<IPackageCache>()
                .To<PackageCache>()
                .InSingletonScope();
        }
    }
}
