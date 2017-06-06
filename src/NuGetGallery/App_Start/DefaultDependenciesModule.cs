// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;
using NuGetGallery.Diagnostics;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Security;

namespace NuGetGallery
{
    public class DefaultDependenciesModule : Module
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:CyclomaticComplexity", Justification = "This code is more maintainable in the same function.")]
        protected override void Load(ContainerBuilder builder)
        {
            var diagnosticsService = new DiagnosticsService();
            builder.RegisterInstance(diagnosticsService)
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();

            var configuration = new ConfigurationService(new SecretReaderFactory(diagnosticsService));

            builder.RegisterInstance(configuration)
                .AsSelf()
                .As<PoliteCaptcha.IConfigurationSource>();

            builder.RegisterInstance(configuration)
                .AsSelf()
                .As<IGalleryConfigurationService>();

            builder.Register(c => configuration.Current)
                .AsSelf()
                .As<IAppConfiguration>();

            // Force the read of this configuration, so it will be initialized on startup
            builder.Register(c => configuration.Features)
               .AsSelf()
               .As<FeatureConfiguration>();

            builder.RegisterType<TelemetryService>().As<ITelemetryService>().SingleInstance();
            builder.RegisterType<CredentialBuilder>().As<ICredentialBuilder>().SingleInstance();
            builder.RegisterType<CredentialValidator>().As<ICredentialValidator>().SingleInstance();

            builder.RegisterInstance(LuceneCommon.GetDirectory(configuration.Current.LuceneIndexLocation))
                .As<Lucene.Net.Store.Directory>()
                .SingleInstance();

            ConfigureSearch(builder, configuration);

            if (!string.IsNullOrEmpty(configuration.Current.AzureStorageConnectionString))
            {
                builder.RegisterInstance(new TableErrorLog(configuration.Current.AzureStorageConnectionString))
                    .As<ErrorLog>()
                    .SingleInstance();
            }
            else
            {
                builder.RegisterInstance(new SqlErrorLog(configuration.Current.SqlConnectionString))
                    .As<ErrorLog>()
                    .SingleInstance();
            }

            builder.RegisterType<DateTimeProvider>().AsSelf().As<IDateTimeProvider>().SingleInstance();

            builder.RegisterType<HttpContextCacheService>()
                .AsSelf()
                .As<ICacheService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ContentService>()
                .AsSelf()
                .As<IContentService>()
                .SingleInstance();

            builder.Register(c => new EntitiesContext(configuration.Current.SqlConnectionString, readOnly: configuration.Current.ReadOnlyMode))
                .AsSelf()
                .As<IEntitiesContext>()
                .As<DbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<CuratedFeed>>()
                .AsSelf()
                .As<IEntityRepository<CuratedFeed>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<CuratedPackage>>()
                .AsSelf()
                .As<IEntityRepository<CuratedPackage>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageRegistration>>()
                .AsSelf()
                .As<IEntityRepository<PackageRegistration>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Package>>()
                .AsSelf()
                .As<IEntityRepository<Package>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageDependency>>()
                .AsSelf()
                .As<IEntityRepository<PackageDependency>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageDelete>>()
                .AsSelf()
                .As<IEntityRepository<PackageDelete>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Credential>>()
                .AsSelf()
                .As<IEntityRepository<Credential>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageOwnerRequest>>()
                .AsSelf()
                .As<IEntityRepository<PackageOwnerRequest>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CuratedFeedService>()
                .AsSelf()
                .As<ICuratedFeedService>()
                .InstancePerLifetimeScope();

            builder.Register(c => new SupportRequestDbContext(configuration.Current.SqlConnectionStringSupportRequest))
                .AsSelf()
                .As<ISupportRequestDbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SupportRequestService>()
                .AsSelf()
                .As<ISupportRequestService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UserService>()
                .AsSelf()
                .As<IUserService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageNamingConflictValidator>()
                .AsSelf()
                .As<IPackageNamingConflictValidator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageService>()
                .AsSelf()
                .As<IPackageService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageDeleteService>()
                .AsSelf()
                .As<IPackageDeleteService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EditPackageService>()
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<FormsAuthenticationService>()
                .As<IFormsAuthenticationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CookieTempDataProvider>()
                .As<ITempDataProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<NuGetExeDownloaderService>()
                .AsSelf()
                .As<INuGetExeDownloaderService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<StatusService>()
                .AsSelf()
                .As<IStatusService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SecurityPolicyService>()
                .AsSelf()
                .As<ISecurityPolicyService>()
                .InstancePerLifetimeScope();
            
            builder.RegisterType<SecurePushSubscription>()
                .SingleInstance();

            builder.RegisterType<RequireSecurePushForCoOwnersPolicy>()
                .SingleInstance();

            var mailSenderThunk = new Lazy<IMailSender>(
                () =>
                {
                    var settings = configuration;
                    if (settings.Current.SmtpUri != null && settings.Current.SmtpUri.IsAbsoluteUri)
                    {
                        var smtpUri = new SmtpUri(settings.Current.SmtpUri);

                        var mailSenderConfiguration = new MailSenderConfiguration
                        {
                            DeliveryMethod = SmtpDeliveryMethod.Network,
                            Host = smtpUri.Host,
                            Port = smtpUri.Port,
                            EnableSsl = smtpUri.Secure
                        };

                        if (!string.IsNullOrWhiteSpace(smtpUri.UserName))
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

            builder.Register(c => mailSenderThunk.Value)
                .AsSelf()
                .As<IMailSender>()
                .InstancePerLifetimeScope();

            builder.RegisterType<MessageService>()
                .AsSelf()
                .As<IMessageService>()
                .InstancePerLifetimeScope();

            builder.Register(c => HttpContext.Current.User)
                .AsSelf()
                .As<IPrincipal>()
                .InstancePerLifetimeScope();

            IAuditingService defaultAuditingService = null;

            switch (configuration.Current.StorageType)
            {
                case StorageType.FileSystem:
                case StorageType.NotSpecified:
                    ConfigureForLocalFileSystem(builder, configuration);
                    defaultAuditingService = GetAuditingServiceForLocalFileSystem(configuration);
                    break;
                case StorageType.AzureStorage:
                    ConfigureForAzureStorage(builder, configuration);
                    defaultAuditingService = GetAuditingServiceForAzureStorage(configuration);
                    break;
            }

            RegisterAuditingServices(builder, defaultAuditingService);

            builder.RegisterType<FileSystemService>()
                .AsSelf()
                .As<IFileSystemService>()
                .SingleInstance();

            builder.RegisterType<PackageFileService>()
                .AsSelf()
                .As<IPackageFileService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<UploadFileService>()
                .AsSelf()
                .As<IUploadFileService>()
                .InstancePerLifetimeScope();

            // todo: bind all package curators by convention
            builder.RegisterType<WebMatrixPackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<Windows8PackageCurator>()
                .AsSelf()
                .As<IAutomaticPackageCurator>()
                .InstancePerLifetimeScope();

            // todo: bind all commands by convention
            builder.RegisterType<AutomaticallyCuratePackageCommand>()
                .AsSelf()
                .As<IAutomaticallyCuratePackageCommand>()
                .InstancePerLifetimeScope();

            if (configuration.Current.Environment == Constants.DevelopmentEnvironment)
            {
                builder.RegisterType<AllowLocalHttpRedirectPolicy>()
                    .As<ISourceDestinationRedirectPolicy>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<NoLessSecureDestinationRedirectPolicy>()
                    .As<ISourceDestinationRedirectPolicy>()
                    .InstancePerLifetimeScope();
            }

            ConfigureAutocomplete(builder, configuration);
        }

        private static void ConfigureSearch(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            if (configuration.Current.ServiceDiscoveryUri == null)
            {
                builder.RegisterType<LuceneSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .InstancePerLifetimeScope();
                builder.RegisterType<LuceneIndexingService>()
                    .AsSelf()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<ExternalSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .As<IIndexingService>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureAutocomplete(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            if (configuration.Current.ServiceDiscoveryUri != null &&
                !string.IsNullOrEmpty(configuration.Current.AutocompleteServiceResourceType))
            {
                builder.RegisterType<AutoCompleteServicePackageIdsQuery>()
                    .AsSelf()
                    .As<IAutoCompletePackageIdsQuery>()
                    .SingleInstance();

                builder.RegisterType<AutoCompleteServicePackageVersionsQuery>()
                    .AsSelf()
                    .As<IAutoCompletePackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<AutoCompleteDatabasePackageIdsQuery>()
                    .AsSelf()
                    .As<IAutoCompletePackageIdsQuery>()
                    .InstancePerLifetimeScope();

                builder.RegisterType<AutoCompleteDatabasePackageVersionsQuery>()
                    .AsSelf()
                    .As<IAutoCompletePackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureForLocalFileSystem(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            builder.RegisterType<FileSystemFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();

            builder.RegisterInstance(NullReportService.Instance)
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            builder.RegisterInstance(NullStatisticsService.Instance)
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();

            // If we're not using azure storage, then aggregate stats comes from SQL
            builder.RegisterType<SqlAggregateStatsService>()
                .AsSelf()
                .As<IAggregateStatsService>()
                .InstancePerLifetimeScope();
        }

        private static void ConfigureForAzureStorage(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            builder.RegisterInstance(new CloudBlobClientWrapper(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<ICloudBlobClient>()
                .SingleInstance();

            builder.RegisterType<CloudBlobFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .SingleInstance();

            // when running on Windows Azure, we use a back-end job to calculate stats totals and store in the blobs
            builder.RegisterInstance(new JsonAggregateStatsService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IAggregateStatsService>()
                .SingleInstance();

            // when running on Windows Azure, pull the statistics from the warehouse via storage
            builder.RegisterInstance(new CloudReportService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IReportService>()
                .SingleInstance();

            // when running on Windows Azure, download counts come from the downloads.v1.json blob
            var downloadCountService = new CloudDownloadCountService(configuration.Current.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant);
            builder.RegisterInstance(downloadCountService)
                .AsSelf()
                .As<IDownloadCountService>()
                .SingleInstance();
            ObjectMaterializedInterception.AddInterceptor(new DownloadCountObjectMaterializedInterceptor(downloadCountService));

            builder.RegisterType<JsonStatisticsService>()
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();
        }

        private static IAuditingService GetAuditingServiceForLocalFileSystem(IGalleryConfigurationService configuration)
        {
            var auditingPath = Path.Combine(
                FileSystemFileStorageService.ResolvePath(configuration.Current.FileStorageDirectory),
                FileSystemAuditingService.DefaultContainerName);

            return new FileSystemAuditingService(auditingPath, AuditActor.GetAspNetOnBehalfOfAsync);
        }

        private static IAuditingService GetAuditingServiceForAzureStorage(IGalleryConfigurationService configuration)
        {
            string instanceId;
            try
            {
                instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            }
            catch
            {
                instanceId = Environment.MachineName;
            }

            var localIp = AuditActor.GetLocalIpAddressAsync().Result;

            return new CloudAuditingService(instanceId, localIp, configuration.Current.AzureStorageConnectionString, AuditActor.GetAspNetOnBehalfOfAsync);
        }

        private static IAuditingService CombineServices(IEnumerable<IAuditingService> services)
        {
            if (!services.Any())
            {
                return null;
            }

            if (services.Count() == 1)
            {
                return services.First();
            }

            return new AggregateAuditingService(services);
        }

        private static void RegisterAuditingServices(ContainerBuilder builder, IAuditingService defaultAuditingService)
        {
            var addInsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "add-ins");

            using (var serviceProvider = RuntimeServiceProvider.Create(addInsDirectoryPath))
            {
                var auditingServices = serviceProvider.GetExportedValues<IAuditingService>();
                var services = new List<IAuditingService>(auditingServices);

                if (defaultAuditingService != null)
                {
                    services.Add(defaultAuditingService);
                }

                var service = CombineServices(services);

                builder.RegisterInstance(service)
                    .AsSelf()
                    .As<IAuditingService>()
                    .SingleInstance();
            }
        }
    }
}