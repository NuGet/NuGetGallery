using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Ninject;
using Ninject.Modules;
using Ninject.Web.Mvc.Filter;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class ContainerBindings : NinjectModule
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        public override void Load()
        {
            var configuration = new Configuration();
            Bind<IConfiguration>()
                .ToMethod(context => configuration);
            Bind<PoliteCaptcha.IConfigurationSource>()
                .ToMethod(context => Configuration.GetPoliteCaptchaConfiguration());

            Bind<Lucene.Net.Store.Directory>()
                .ToMethod(_ => LuceneCommon.GetDirectory())
                .InSingletonScope();

            Bind<ISearchService>()
                .To<LuceneSearchService>()
                .InRequestScope();

            if (!String.IsNullOrEmpty(configuration.AzureDiagnosticsConnectionString))
            {
                Bind<ErrorLog>()
                    .ToMethod(_ => new TableErrorLog(configuration.AzureDiagnosticsConnectionString))
                    .InSingletonScope();
            }
            else
            {
                Bind<ErrorLog>()
                    .ToMethod(_ => new SqlErrorLog(configuration.SqlConnectionString))
                    .InSingletonScope();
            }
            
            if (IsDeployedToCloud)
            {
                // when running on Windows Azure, use the Azure Cache service if available
                if (!String.IsNullOrEmpty(configuration.AzureCacheEndpoint))
                {
                    Bind<ICacheService>()
                        .To<CloudCacheService>()
                        .InSingletonScope();
                }
                else
                {
                    Bind<ICacheService>()
                        .To<HttpContextCacheService>()
                        .InRequestScope();
                }

                // when running on Windows Azure, pull the statistics from the warehouse via storage
                Bind<IReportService>()
                    .ToMethod(context => new CloudReportService(configuration.AzureStatisticsConnectionString))
                    .InSingletonScope();

                Bind<IStatisticsService>()
                    .To<JsonStatisticsService>()
                    .InSingletonScope();
            }
            else
            {
                // when running locally on dev box, use the built-in ASP.NET Http Cache
                Bind<ICacheService>()
                    .To<HttpContextCacheService>()
                    .InRequestScope();
            }

            Bind<IEntitiesContext>()
                .ToMethod(context => new EntitiesContext(configuration.SqlConnectionString, readOnly: configuration.ReadOnlyMode))
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
                        var settings = Kernel.Get<IConfiguration>();
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
                            _ => new CloudBlobClientWrapper(configuration.AzureStorageConnectionString))
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
            Bind<ICuratedFeedService>()
                .To<CuratedFeedService>()
                .InRequestScope();
            Bind<IPackageRegistrationByKeyQuery>()
                .To<PackageRegistrationByKeyQuery>()
                .InRequestScope();
            Bind<IPackageRegistrationByIdQuery>()
                .To<PackageRegistrationByIdQuery>()
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

        public static bool IsDeployedToCloud
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
