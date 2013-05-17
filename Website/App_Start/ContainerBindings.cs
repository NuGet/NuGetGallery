using System;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using Ninject;
using Ninject.Modules;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure;

namespace NuGetGallery
{
    public class ContainerBindings : NinjectModule
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        public override void Load()
        {
            var configuration = new ConfigurationService();
            Bind<ConfigurationService>()
                .ToMethod(context => configuration);
            Bind<IAppConfiguration>()
                .ToMethod(context => configuration.Current);
            Bind<PoliteCaptcha.IConfigurationSource>()
                .ToMethod(context => configuration);

            Bind<Lucene.Net.Store.Directory>()
                .ToMethod(_ => LuceneCommon.GetDirectory())
                .InSingletonScope();

            Bind<ISearchService>()
                .To<LuceneSearchService>()
                .InRequestScope();

            if (!String.IsNullOrEmpty(configuration.Current.AzureStorageConnectionString))
            {
                Bind<ErrorLog>()
                    .ToMethod(_ => new TableErrorLog(configuration.Current.AzureStorageConnectionString))
                    .InSingletonScope();
            }
            else
            {
                Bind<ErrorLog>()
                    .ToMethod(_ => new SqlErrorLog(configuration.Current.SqlConnectionString))
                    .InSingletonScope();
            }

            // when running on Windows Azure, use the Azure Cache service if available
            if (!String.IsNullOrEmpty(configuration.Current.AzureCacheConnectionString))
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

            if (!String.IsNullOrEmpty(configuration.Current.AzureStorageConnectionString))
            {
                // when running on Windows Azure, pull the statistics from the warehouse via storage
                Bind<IReportService>()
                    .ToMethod(context => new CloudReportService(configuration.Current.AzureStorageConnectionString))
                    .InSingletonScope();

                Bind<IStatisticsService>()
                    .To<JsonStatisticsService>()
                    .InSingletonScope();
            }

            Bind<IContentService>()
                .To<ContentService>()
                .InSingletonScope();

            Bind<IEntitiesContext>()
                .ToMethod(context => new EntitiesContext(configuration.Current.SqlConnectionString, readOnly: configuration.Current.ReadOnlyMode))
                .InRequestScope();

            Bind<IEntityRepository<User>>()
                .To<EntityRepository<User>>()
                .InRequestScope();

            Bind<IEntityRepository<CuratedFeed>>()
                .To<EntityRepository<CuratedFeed>>()
                .InRequestScope();

            Bind<IEntityRepository<CuratedPackage>>()
                .To<EntityRepository<CuratedPackage>>()
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

            Bind<ICuratedFeedService>()
                .To<CuratedFeedService>()
                .InRequestScope();

            Bind<IUserService>()
                .To<UserService>()
                .InRequestScope();

            Bind<IPackageService>()
                .To<PackageService>()
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
                    var settings = Kernel.Get<ConfigurationService>();
                    if (settings.Current.SmtpUri != null)
                    {
                        var smtpUri = new SmtpUri(settings.Current.SmtpUri);

                        var mailSenderConfiguration = new MailSenderConfiguration
                            {
                                DeliveryMethod = SmtpDeliveryMethod.Network,
                                Host = smtpUri.Host,
                                Port = smtpUri.Port,
                                EnableSsl = smtpUri.Secure
                            };

                        if (!String.IsNullOrWhiteSpace(smtpUri.UserName))
                        {
                            mailSenderConfiguration.UseDefaultCredentials = false;
                            mailSenderConfiguration.Credentials = new NetworkCredential(
                                smtpUri.UserName,
                                smtpUri.Password);
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

            switch (configuration.Current.StorageType)
            {
                case StorageType.FileSystem:
                case StorageType.NotSpecified:
                    Bind<IFileStorageService>()
                        .To<FileSystemFileStorageService>()
                        .InSingletonScope();
                    break;
                case StorageType.AzureStorage:
                    Bind<ICloudBlobClient>()
                        .ToMethod(
                            _ => new CloudBlobClientWrapper(configuration.Current.AzureStorageConnectionString))
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
    }
}
