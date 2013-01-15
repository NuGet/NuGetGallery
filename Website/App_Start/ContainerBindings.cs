using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Ninject;
using Ninject.Modules;

namespace NuGetGallery
{
    public class ContainerBindings : NinjectModule
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        public override void Load()
        {
            IConfiguration configuration = new Configuration();
            Bind<IConfiguration>()
                .ToMethod(context => configuration);

            var gallerySetting = new Lazy<GallerySetting>(
                () =>
                    {
                        using (var entitiesContext = new EntitiesContext())
                        {
                            var settingsRepo = new EntityRepository<GallerySetting>(entitiesContext);
                            return settingsRepo.GetAll().FirstOrDefault();
                        }
                    });

            Bind<GallerySetting>().ToMethod(c => gallerySetting.Value);

            Bind<Lucene.Net.Store.Directory>()
                .ToMethod((_) => LuceneCommon.GetDirectory())
                .InSingletonScope();

            Bind<ISearchService>()
                .To<LuceneSearchService>()
                .InRequestScope();
            
            if (IsDeployedToCloud)
            {
                // when running on Windows Azure, use the Azure Cache local storage
                Bind<IPackageCacheService>()
                    .To<CloudPackageCacheService>()
                    .InSingletonScope();

                // when running on Windows Azure, use the Azure Cache service
                Bind<ICacheService>()
                    .To<CloudCacheService>()
                    .InSingletonScope();

                // when running on Windows Azure, get the statistics from Azure blob storage
                Bind<IStatisticsService>()
                    .ToMethod(context => new CloudStatisticsService(configuration.AzureStatisticsConnectionString))
                    .InSingletonScope();
            }
            else
            {
                Bind<IPackageCacheService>()
                    .To<NullPackageCacheService>()
                    .InSingletonScope();

                // when running locally on dev box, use the built-in ASP.NET Http Cache
                Bind<ICacheService>()
                    .To<HttpContextCacheService>()
                    .InRequestScope();

                // when running locally on dev box, get the local statistics (a more limited sets of stats are available)
                Bind<IStatisticsService>()
                    //.To<LocalStatisticsService>()
                    .ToMethod(context => new CloudStatisticsService(configuration.AzureStatisticsConnectionString))
                    .InSingletonScope();
            }

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

            Bind<IPackageSource>()
                .To<PackageSource>()
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

            var mailSenderThunk = new Lazy<IMailSender>(
                () =>
                    {
                        var settings = Kernel.Get<GallerySetting>();
                        if (settings.UseSmtp)
                        {
                            var mailSenderConfiguration = new MailSenderConfiguration
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
                            var mailSenderConfiguration = new MailSenderConfiguration
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
                    break;
                case PackageStoreType.AzureStorageBlob:
                    Bind<ICloudBlobClient>()
                        .ToMethod(
                            context => new CloudBlobClientWrapper(
                                           new CloudBlobClient(
                                               new Uri(configuration.AzureStorageBlobUrl, UriKind.Absolute),
                                               configuration.UseEmulator
                                                   ? CloudStorageAccount.DevelopmentStorageAccount.Credentials
                                                   : new StorageCredentials(
                                                         configuration.AzureStorageAccountName, configuration.AzureStorageAccessKey))))
                        .InSingletonScope();
                    Bind<IFileStorageService>()
                        .To<CloudBlobFileStorageService>()
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

            // todo: bind all package curators by convention
            Bind<IAutomaticPackageCurator>()
                .To<WebMatrixPackageCurator>();
            Bind<IAutomaticPackageCurator>()
                .To<Windows8PackageCurator>();

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

            Bind<IAggregateStatsService>()
                .To<AggregateStatsService>()
                .InRequestScope();
            Bind<IPackageIdsQuery>()
                .To<PackageIdsQuery>()
                .InRequestScope();
            Bind<IPackageVersionsQuery>()
                .To<PackageVersionsQuery>()
                .InRequestScope();
        }

        private static bool IsDeployedToCloud
        {
            get
            {
                try
                {
                    if (RoleEnvironment.IsAvailable)
                    {
                        return true;
                    }
                }
                catch (TypeInitializationException)
                {
                    // Catch 'Could not load file or assembly 'msshrtmi' from not having Azure SDK installed.
                }

                return false;
            }
        }
    }
}