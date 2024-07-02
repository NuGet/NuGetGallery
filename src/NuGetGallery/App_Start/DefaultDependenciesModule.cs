// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Interception;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Configuration;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGet.Services.KeyVault;
using NuGet.Services.Licenses;
using NuGet.Services.Logging;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGet.Services.Sql;
using NuGet.Services.Validation;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.Services;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Cookies;
using NuGetGallery.Diagnostics;
using NuGetGallery.Features;
using NuGetGallery.Frameworks;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Infrastructure.Mail;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.Infrastructure.Search.Correlation;
using NuGetGallery.Security;
using NuGetGallery.Services;
using Role = NuGet.Services.Entities.Role;

namespace NuGetGallery
{
    public class DefaultDependenciesModule : Module
    {
        public static class BindingKeys
        {
            public const string AsyncDeleteAccountName = "AsyncDeleteAccountService";
            public const string SyncDeleteAccountName = "SyncDeleteAccountService";

            public const string PrimaryStatisticsKey = "PrimaryStatisticsKey";
            public const string AlternateStatisticsKey = "AlternateStatisticsKey";
            public const string FeatureFlaggedStatisticsKey = "FeatureFlaggedStatisticsKey";

            public const string AccountDeleterTopic = "AccountDeleterBindingKey";
            public const string PackageValidationTopic = "PackageValidationBindingKey";
            public const string SymbolsPackageValidationTopic = "SymbolsPackageValidationBindingKey";
            public const string PackageValidationEnqueuer = "PackageValidationEnqueuerBindingKey";
            public const string SymbolsPackageValidationEnqueuer = "SymbolsPackageValidationEnqueuerBindingKey";
            public const string EmailPublisherTopic = "EmailPublisherBindingKey";

            public const string PreviewSearchClient = "PreviewSearchClientBindingKey";

            public const string AuditKey = "AuditKey";
        }

        public static class ParameterNames
        {
            public const string PackagesController_PreviewSearchService = "previewSearchService";
        }

        protected override void Load(ContainerBuilder builder)
        {
            var services = new ServiceCollection();

            var configuration = ConfigurationService.Initialize();
            var secretInjector = configuration.SecretInjector;

            builder.RegisterInstance(secretInjector)
                .AsSelf()
                .As<ISecretInjector>()
                .SingleInstance();

            // Register the ILoggerFactory and configure AppInsights.
            var applicationInsightsConfiguration = ConfigureApplicationInsights(
                configuration.Current,
                out ITelemetryClient telemetryClient);

            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: false);
            var loggerFactory = LoggingSetup.CreateLoggerFactory(
                loggerConfiguration,
                telemetryConfiguration: applicationInsightsConfiguration.TelemetryConfiguration);

            ActionsRequiringPermissions.AdminAccessEnabled = configuration.Current.AdminPanelEnabled;

            builder.RegisterInstance(applicationInsightsConfiguration.TelemetryConfiguration)
                .AsSelf()
                .SingleInstance();

            builder.RegisterInstance(telemetryClient)
                .AsSelf()
                .As<ITelemetryClient>()
                .SingleInstance();

            var diagnosticsService = new DiagnosticsService(telemetryClient);
            builder.RegisterInstance(diagnosticsService)
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();

            services.AddSingleton(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            UrlHelperExtensions.SetConfigurationService(configuration);
            builder.RegisterType<UrlHelperWrapper>()
                .As<IUrlHelper>()
                .InstancePerLifetimeScope();

            builder.RegisterInstance(configuration)
                .AsSelf()
                .As<IConfigurationFactory>()
                .As<IGalleryConfigurationService>();

            builder.Register(c => configuration.Current)
                .AsSelf()
                .AsImplementedInterfaces();

            // Force the read of this configuration, so it will be initialized on startup
            builder.Register(c => configuration.Features)
               .AsSelf()
               .As<FeatureConfiguration>();

            builder.Register(c => configuration.PackageDelete)
                .As<IPackageDeleteConfiguration>();

            var telemetryService = new TelemetryService(
                new TraceDiagnosticsSource(nameof(TelemetryService), telemetryClient),
                telemetryClient);

            builder.RegisterInstance(telemetryService)
                .AsSelf()
                .As<ITelemetryService>()
                .As<IFeatureFlagTelemetryService>()
                .SingleInstance();

            builder.RegisterType<CredentialBuilder>().As<ICredentialBuilder>().SingleInstance();
            builder.RegisterType<CredentialValidator>().As<ICredentialValidator>().SingleInstance();

            builder.RegisterInstance(LuceneCommon.GetDirectory(configuration.Current.LuceneIndexLocation))
                .As<Lucene.Net.Store.Directory>()
                .SingleInstance();

            ConfigureSearch(loggerFactory, configuration, telemetryService, services, builder);

            builder.RegisterType<DateTimeProvider>().AsSelf().As<IDateTimeProvider>().SingleInstance();

            builder.RegisterType<HttpContextCacheService>()
                .AsSelf()
                .As<ICacheService>()
                .InstancePerLifetimeScope();

            DbInterception.Add(new QueryHintInterceptor());

            var galleryDbConnectionFactory = CreateDbConnectionFactory(
                loggerFactory,
                nameof(EntitiesContext),
                configuration.Current.SqlConnectionString,
                secretInjector);

            builder.RegisterInstance(galleryDbConnectionFactory)
                .AsSelf()
                .As<ISqlConnectionFactory>()
                .SingleInstance();

            builder.Register(c => new EntitiesContext(CreateDbConnection(galleryDbConnectionFactory, telemetryService), configuration.Current.ReadOnlyMode))
                .AsSelf()
                .As<IEntitiesContext>()
                .As<DbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Role>>()
                .AsSelf()
                .As<IEntityRepository<Role>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<ReservedNamespace>>()
                .AsSelf()
                .As<IEntityRepository<ReservedNamespace>>()
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

            builder.RegisterType<EntityRepository<Certificate>>()
                .AsSelf()
                .As<IEntityRepository<Certificate>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<AccountDelete>>()
               .AsSelf()
               .As<IEntityRepository<AccountDelete>>()
               .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Credential>>()
                .AsSelf()
                .As<IEntityRepository<Credential>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Scope>>()
                .AsSelf()
                .As<IEntityRepository<Scope>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageOwnerRequest>>()
                .AsSelf()
                .As<IEntityRepository<PackageOwnerRequest>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<Organization>>()
                .AsSelf()
                .As<IEntityRepository<Organization>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<SymbolPackage>>()
                .AsSelf()
                .As<IEntityRepository<SymbolPackage>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageDeprecation>>()
                .AsSelf()
                .As<IEntityRepository<PackageDeprecation>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<PackageRename>>()
                .AsSelf()
                .As<IEntityRepository<PackageRename>>()
                .InstancePerLifetimeScope();

            ConfigureGalleryReadOnlyReplicaEntitiesContext(builder, loggerFactory, configuration, secretInjector, telemetryService);

            var supportDbConnectionFactory = CreateDbConnectionFactory(
                loggerFactory,
                nameof(SupportRequestDbContext),
                configuration.Current.SqlConnectionStringSupportRequest,
                secretInjector);

            builder.Register(c => new SupportRequestDbContext(CreateDbConnection(supportDbConnectionFactory, telemetryService)))
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

            builder.RegisterType<PackageService>()
                .AsSelf()
                .As<IPackageService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageFilter>()
                .AsSelf()
                .As<IPackageFilter>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageDeleteService>()
                .AsSelf()
                .As<IPackageDeleteService>()
                .InstancePerLifetimeScope();

            RegisterDeleteAccountService(builder, configuration);

            builder.RegisterType<PackageOwnerRequestService>()
                .AsSelf()
                .As<IPackageOwnerRequestService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<FormsAuthenticationService>()
                .As<IFormsAuthenticationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CookieTempDataProvider>()
                .As<ITempDataProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<StatusService>()
                .AsSelf()
                .As<IStatusService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SecurityPolicyService>()
                .AsSelf()
                .As<ISecurityPolicyService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ReservedNamespaceService>()
                .AsSelf()
                .As<IReservedNamespaceService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SymbolPackageService>()
                .AsSelf()
                .As<ISymbolPackageService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageMetadataValidationService>()
                .As<IPackageMetadataValidationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageUploadService>()
                .AsSelf()
                .As<IPackageUploadService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<SymbolPackageUploadService>()
                .AsSelf()
                .As<ISymbolPackageUploadService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageOwnershipManagementService>()
                .AsSelf()
                .As<IPackageOwnershipManagementService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ValidationService>()
                .AsSelf()
                .As<IValidationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ReadMeService>()
                .AsSelf()
                .As<IReadMeService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<MarkdownService>()
                .As<IMarkdownService>()
                .InstancePerLifetimeScope();
            
            builder.RegisterType<ImageDomainValidator>()
                .As<IImageDomainValidator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ApiScopeEvaluator>()
                .AsSelf()
                .As<IApiScopeEvaluator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ContentObjectService>()
                .AsSelf()
                .As<IContentObjectService>()
                .SingleInstance();

            builder.RegisterType<CertificateValidator>()
                .AsSelf()
                .As<ICertificateValidator>()
                .SingleInstance();

            builder.RegisterType<CertificateService>()
                .AsSelf()
                .As<ICertificateService>()
                .InstancePerLifetimeScope();
            
            RegisterTyposquattingServiceHelper(builder, loggerFactory);

            builder.RegisterType<TyposquattingService>()
                .AsSelf()
                .As<ITyposquattingService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<TyposquattingCheckListCacheService>()
                .AsSelf()
                .As<ITyposquattingCheckListCacheService>()
                .SingleInstance();

            builder.RegisterType<LicenseExpressionSplitter>()
                .As<ILicenseExpressionSplitter>()
                .InstancePerLifetimeScope();

            builder.RegisterType<LicenseExpressionParser>()
                .As<ILicenseExpressionParser>()
                .InstancePerLifetimeScope();

            builder.RegisterType<LicenseExpressionSegmentator>()
                .As<ILicenseExpressionSegmentator>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageDeprecationManagementService>()
                .As<IPackageDeprecationManagementService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageDeprecationService>()
                .As<IPackageDeprecationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageVulnerabilitiesService>()
                .As<IPackageVulnerabilitiesService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageRenameService>()
                .As<IPackageRenameService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageUpdateService>()
                .As<IPackageUpdateService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<GalleryCloudBlobContainerInformationProvider>()
                .As<ICloudBlobContainerInformationProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ConfigurationIconFileProvider>()
                .As<IIconUrlProvider>()
                .InstancePerLifetimeScope();

            builder.RegisterType<IconUrlTemplateProcessor>()
                .As<IIconUrlTemplateProcessor>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageVulnerabilitiesManagementService>()
                .As<IPackageVulnerabilitiesManagementService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<PackageVulnerabilitiesCacheService>()
                .AsSelf()
                .As<IPackageVulnerabilitiesCacheService>()
                .SingleInstance();

            builder.RegisterType<PackageFrameworkCompatibilityFactory>()
                .AsSelf()
                .As<IPackageFrameworkCompatibilityFactory>()
                .SingleInstance();

            services.AddHttpClient();
            services.AddScoped<IGravatarProxyService, GravatarProxyService>();

            RegisterFeatureFlagsService(builder, configuration);
            RegisterMessagingService(builder, configuration);

            builder.Register(c => HttpContext.Current.User)
                .AsSelf()
                .As<IPrincipal>()
                .InstancePerLifetimeScope();

            switch (configuration.Current.StorageType)
            {
                case StorageType.FileSystem:
                case StorageType.NotSpecified:
                    ConfigureForLocalFileSystem(builder, configuration);
                    break;
                case StorageType.AzureStorage:
                    ConfigureForAzureStorage(builder, configuration, telemetryService);
                    break;
            }

            RegisterAsynchronousValidation(builder, loggerFactory, configuration, secretInjector, telemetryService);

            RegisterAuditingServices(builder, configuration.Current.StorageType);

            RegisterCookieComplianceService(configuration, loggerFactory);

            builder.RegisterType<CookieExpirationService>()
                .As<ICookieExpirationService>()
                .SingleInstance();

            RegisterABTestServices(builder);

            builder.RegisterType<MicrosoftTeamSubscription>()
                .AsSelf()
                .InstancePerLifetimeScope();

            if (configuration.Current.Environment == ServicesConstants.DevelopmentEnvironment)
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
            builder.Populate(services);
        }

        // Internal for testing purposes
        internal static ApplicationInsightsConfiguration ConfigureApplicationInsights(
            IAppConfiguration configuration,
            out ITelemetryClient telemetryClient)
        {
            var instrumentationKey = configuration.AppInsightsInstrumentationKey;
            var heartbeatIntervalSeconds = configuration.AppInsightsHeartbeatIntervalSeconds;

            ApplicationInsightsConfiguration applicationInsightsConfiguration;

            if (heartbeatIntervalSeconds > 0)
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(
                    instrumentationKey,
                    TimeSpan.FromSeconds(heartbeatIntervalSeconds));
            }
            else
            {
                applicationInsightsConfiguration = ApplicationInsights.Initialize(instrumentationKey);
            }

            var telemetryConfiguration = applicationInsightsConfiguration.TelemetryConfiguration;

            // Add enrichers
            try
            {
                if (RoleEnvironment.IsAvailable)
                {
                    telemetryConfiguration.TelemetryInitializers.Add(
                        new DeploymentIdTelemetryEnricher(RoleEnvironment.DeploymentId));
                }
            }
            catch
            {
                // This likely means the cloud service runtime is not available.
            }

            if (configuration.DeploymentLabel != null)
            {
                telemetryConfiguration.TelemetryInitializers.Add(new DeploymentLabelEnricher(configuration.DeploymentLabel));
            }

            telemetryConfiguration.TelemetryInitializers.Add(new ClientInformationTelemetryEnricher());
            telemetryConfiguration.TelemetryInitializers.Add(new KnownOperationNameEnricher());
            telemetryConfiguration.TelemetryInitializers.Add(new AzureWebAppTelemetryInitializer());
            telemetryConfiguration.TelemetryInitializers.Add(new CustomerResourceIdEnricher());

            // Add processors
            telemetryConfiguration.TelemetryProcessorChainBuilder.Use(next =>
            {
                var processor = new RequestTelemetryProcessor(next);

                processor.SuccessfulResponseCodes.Add(400);
                processor.SuccessfulResponseCodes.Add(404);
                processor.SuccessfulResponseCodes.Add(405);

                return processor;
            });

            telemetryConfiguration.TelemetryProcessorChainBuilder.Use(next => new ClientTelemetryPIIProcessor(next));

            // Hook-up TelemetryModules manually...
            RegisterApplicationInsightsTelemetryModules(telemetryConfiguration);

            var telemetryClientWrapper = TelemetryClientWrapper.UseTelemetryConfiguration(applicationInsightsConfiguration.TelemetryConfiguration);

            telemetryConfiguration.TelemetryProcessorChainBuilder.Use(
                next => new ExceptionTelemetryProcessor(next, telemetryClientWrapper.UnderlyingClient));

            // Note: sampling rate must be a factor 100/N where N is a whole number
            // e.g.: 50 (= 100/2), 33.33 (= 100/3), 25 (= 100/4), ...
            // https://azure.microsoft.com/en-us/documentation/articles/app-insights-sampling/
            var instrumentationSamplingPercentage = configuration.AppInsightsSamplingPercentage;
            if (instrumentationSamplingPercentage > 0 && instrumentationSamplingPercentage < 100)
            {
                telemetryConfiguration.TelemetryProcessorChainBuilder.UseSampling(instrumentationSamplingPercentage);
            }

            telemetryConfiguration.TelemetryProcessorChainBuilder.Build();

            telemetryClient = telemetryClientWrapper;

            return applicationInsightsConfiguration;
        }

        private static void RegisterApplicationInsightsTelemetryModules(TelemetryConfiguration configuration)
        {
            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.WindowsServer.AppServicesHeartbeatTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.WindowsServer.AzureInstanceMetadataTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.WindowsServer.DeveloperModeWithDebuggerAttachedTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.WindowsServer.UnhandledExceptionTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.WindowsServer.UnobservedExceptionTelemetryModule(),
                configuration);

            var requestTrackingModule = new Microsoft.ApplicationInsights.Web.RequestTrackingTelemetryModule();
            requestTrackingModule.Handlers.Add("Microsoft.VisualStudio.Web.PageInspector.Runtime.Tracing.RequestDataHttpHandler");
            requestTrackingModule.Handlers.Add("System.Web.StaticFileHandler");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.AssemblyResourceLoader");
            requestTrackingModule.Handlers.Add("System.Web.Optimization.BundleHandler");
            requestTrackingModule.Handlers.Add("System.Web.Script.Services.ScriptHandlerFactory");
            requestTrackingModule.Handlers.Add("System.Web.Handlers.TraceHandler");
            requestTrackingModule.Handlers.Add("System.Web.Services.Discovery.DiscoveryRequestHandler");
            requestTrackingModule.Handlers.Add("System.Web.HttpDebugHandler");
            RegisterApplicationInsightsTelemetryModule(
                requestTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.Web.ExceptionTrackingTelemetryModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.Web.AspNetDiagnosticTelemetryModule(),
                configuration);

            var dependencyTrackingModule = new Microsoft.ApplicationInsights.DependencyCollector.DependencyTrackingTelemetryModule();
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            dependencyTrackingModule.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");
            dependencyTrackingModule.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            RegisterApplicationInsightsTelemetryModule(
                dependencyTrackingModule,
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.PerformanceCollectorModule(),
                configuration);

            RegisterApplicationInsightsTelemetryModule(
                new Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse.QuickPulseTelemetryModule(),
                configuration);
        }

        private static void RegisterApplicationInsightsTelemetryModule(ITelemetryModule telemetryModule, TelemetryConfiguration configuration)
        {
            var existingModule = TelemetryModules.Instance.Modules.SingleOrDefault(m => m.GetType().Equals(telemetryModule.GetType()));
            if (existingModule != null)
            {
                TelemetryModules.Instance.Modules.Remove(existingModule);
            }

            telemetryModule.Initialize(configuration);

            TelemetryModules.Instance.Modules.Add(telemetryModule);
        }

        private void RegisterABTestServices(ContainerBuilder builder)
        {
            builder
                .RegisterType<ABTestEnrollmentFactory>()
                .As<IABTestEnrollmentFactory>();

            builder
                .RegisterType<CookieBasedABTestService>()
                .As<IABTestService>();
        }

        private static void RegisterDeleteAccountService(ContainerBuilder builder, ConfigurationService configuration)
        {
            if (configuration.Current.AsynchronousDeleteAccountServiceEnabled)
            {
                RegisterSwitchingDeleteAccountService(builder, configuration);
            }
            else
            {
                builder.RegisterType<DeleteAccountService>()
                    .AsSelf()
                    .As<IDeleteAccountService>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void RegisterStatisticsServices(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            // when running on Windows Azure, download counts come from the downloads.v1.json blob
            builder.Register(c => new SimpleBlobStorageConfiguration(configuration.Current.AzureStorage_Statistics_ConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .SingleInstance()
                .Keyed<IBlobStorageConfiguration>(BindingKeys.PrimaryStatisticsKey);

            builder.Register(c => new SimpleBlobStorageConfiguration(configuration.Current.AzureStorage_Statistics_ConnectionString_Alternate, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .SingleInstance()
                .Keyed<IBlobStorageConfiguration>(BindingKeys.AlternateStatisticsKey);

            builder.Register(c =>
                {
                    var blobConfiguration = c.ResolveKeyed<IBlobStorageConfiguration>(BindingKeys.PrimaryStatisticsKey);
                    return new CloudBlobClientWrapper(blobConfiguration.ConnectionString, blobConfiguration.ReadAccessGeoRedundant);
                })
                .SingleInstance()
                .Keyed<ICloudBlobClient>(BindingKeys.PrimaryStatisticsKey);

            builder.Register(c =>
                {
                    var blobConfiguration = c.ResolveKeyed<IBlobStorageConfiguration>(BindingKeys.AlternateStatisticsKey);
                    return new CloudBlobClientWrapper(blobConfiguration.ConnectionString, blobConfiguration.ReadAccessGeoRedundant);
                })
                .SingleInstance()
                .Keyed<ICloudBlobClient>(BindingKeys.AlternateStatisticsKey);

            var hasSecondaryStatisticsSource = !string.IsNullOrWhiteSpace(configuration.Current.AzureStorage_Statistics_ConnectionString_Alternate);

            builder.Register(c =>
                {
                    if (hasSecondaryStatisticsSource && c.Resolve<IFeatureFlagService>().IsAlternateStatisticsSourceEnabled())
                    {
                        return c.ResolveKeyed<ICloudBlobClient>(BindingKeys.AlternateStatisticsKey);
                    }
                    return c.ResolveKeyed<ICloudBlobClient>(BindingKeys.PrimaryStatisticsKey);
                })
                .Keyed<ICloudBlobClient>(BindingKeys.FeatureFlaggedStatisticsKey);

            // when running on Windows Azure, we use a back-end job to calculate stats totals and store in the blobs
            builder.Register(c => new JsonAggregateStatsService(c.ResolveKeyed<Func<ICloudBlobClient>>(BindingKeys.FeatureFlaggedStatisticsKey)))
                .As<IAggregateStatsService>()
                .SingleInstance();

            // when running on Windows Azure, pull the statistics from the warehouse via storage
            builder.Register(c => new CloudReportService(c.ResolveKeyed<Func<ICloudBlobClient>>(BindingKeys.FeatureFlaggedStatisticsKey)))
                .As<IReportService>()
                .SingleInstance();

            builder.Register(c =>
                {
                    var cloudBlobClientFactory = c.ResolveKeyed<Func<ICloudBlobClient>>(BindingKeys.FeatureFlaggedStatisticsKey);
                    var telemetryService = c.Resolve<ITelemetryService>();
                    var downloadCountServiceLogger = c.Resolve<ILogger<CloudDownloadCountService>>();
                    var downloadCountService = new CloudDownloadCountService(telemetryService, cloudBlobClientFactory, downloadCountServiceLogger);

                    var dlCountInterceptor = new DownloadCountObjectMaterializedInterceptor(downloadCountService, telemetryService);
                    ObjectMaterializedInterception.AddInterceptor(dlCountInterceptor);

                    return downloadCountService;
                })
                .As<IDownloadCountService>()
                .SingleInstance();

            builder.RegisterType<JsonStatisticsService>()
                .As<IStatisticsService>()
                .SingleInstance();
        }

        private static void RegisterSwitchingDeleteAccountService(ContainerBuilder builder, ConfigurationService configuration)
        {
            var asyncAccountDeleteConnectionString = configuration.ServiceBus.AccountDeleter_ConnectionString;
            var asyncAccountDeleteTopicName = configuration.ServiceBus.AccountDeleter_TopicName;

            builder
                .Register(c => new TopicClientWrapper(asyncAccountDeleteConnectionString, asyncAccountDeleteTopicName, configuration.ServiceBus.ManagedIdentityClientId))
                .SingleInstance()
                .Keyed<ITopicClient>(BindingKeys.AccountDeleterTopic)
                .OnRelease(x => x.CloseAsync());

            builder
                .RegisterType<AsynchronousDeleteAccountService>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<ITopicClient>(BindingKeys.AccountDeleterTopic)));

            builder.RegisterType<DeleteAccountService>();

            builder.RegisterType<AccountDeleteMessageSerializer>()
                .As<IBrokeredMessageSerializer<AccountDeleteMessage>>();

            builder
                .Register<IDeleteAccountService>(c =>
                {
                    var featureFlagService = c.Resolve<IFeatureFlagService>();
                    if (featureFlagService.IsAsyncAccountDeleteEnabled())
                    {
                        return c.Resolve<AsynchronousDeleteAccountService>();
                    }

                    return c.Resolve<DeleteAccountService>();
                })
                .InstancePerLifetimeScope();
        }

        private static void RegisterFeatureFlagsService(ContainerBuilder builder, ConfigurationService configuration)
        {
            builder
                .Register(context => new FeatureFlagOptions
                {
                    RefreshInterval = configuration.Current.FeatureFlagsRefreshInterval,
                })
                .AsSelf()
                .SingleInstance();

            builder
                .Register(context => context.Resolve<EditableFeatureFlagFileStorageService>())
                .As<IEditableFeatureFlagStorageService>()
                .SingleInstance();

            builder
                .RegisterType<FeatureFlagCacheService>()
                .As<IFeatureFlagCacheService>()
                .SingleInstance();

            builder
                .RegisterType<FeatureFlagClient>()
                .As<IFeatureFlagClient>()
                .SingleInstance();

            builder
                .RegisterType<FeatureFlagService>()
                .As<IFeatureFlagService>()
                .SingleInstance();
        }

        private static void RegisterMessagingService(ContainerBuilder builder, ConfigurationService configuration)
        {
            if (configuration.Current.AsynchronousEmailServiceEnabled)
            {
                // Register NuGet.Services.Messaging infrastructure
                RegisterAsynchronousEmailMessagingService(builder, configuration);
            }
            else
            {
                // Register legacy SMTP messaging infrastructure
                RegisterSmtpEmailMessagingService(builder, configuration);
            }
        }

        private static void RegisterSmtpEmailMessagingService(ContainerBuilder builder, ConfigurationService configuration)
        {
            MailSender mailSenderFactory()
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
            }

            builder.Register(c => mailSenderFactory())
                .AsSelf()
                .As<IMailSender>()
                .InstancePerDependency();

            builder.RegisterType<BackgroundMarkdownMessageService>()
                .AsSelf()
                .As<IMessageService>()
                .InstancePerDependency();
        }

        private static void RegisterAsynchronousEmailMessagingService(ContainerBuilder builder, ConfigurationService configuration)
        {
            builder
                .RegisterType<NuGet.Services.Messaging.ServiceBusMessageSerializer>()
                .As<NuGet.Services.Messaging.IServiceBusMessageSerializer>();

            var emailPublisherConnectionString = configuration.ServiceBus.EmailPublisher_ConnectionString;
            var emailPublisherTopicName = configuration.ServiceBus.EmailPublisher_TopicName;

            builder
                .Register(c => new TopicClientWrapper(emailPublisherConnectionString, emailPublisherTopicName, configuration.ServiceBus.ManagedIdentityClientId))
                .As<ITopicClient>()
                .SingleInstance()
                .Keyed<ITopicClient>(BindingKeys.EmailPublisherTopic)
                .OnRelease(x => x.CloseAsync());

            builder
                .RegisterType<EmailMessageEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<ITopicClient>(BindingKeys.EmailPublisherTopic)))
                .As<IEmailMessageEnqueuer>();

            builder.RegisterType<AsynchronousEmailMessageService>()
                .AsSelf()
                .As<IMessageService>()
                .InstancePerDependency();
        }

        private static ISqlConnectionFactory CreateDbConnectionFactory(
            ILoggerFactory loggerFactory,
            string name,
            string connectionString,
            ICachingSecretInjector secretInjector)
        {
            var logger = loggerFactory.CreateLogger($"AzureSqlConnectionFactory-{name}");
            return new AzureSqlConnectionFactory(connectionString, secretInjector, logger);
        }

        public static DbConnection CreateDbConnection(ISqlConnectionFactory connectionFactory, ITelemetryService telemetryService)
        {
            using (telemetryService.TrackSyncSqlConnectionCreationDuration())
            {
                if (connectionFactory.TryCreate(out var connection))
                {
                    return connection;
                }
            }
            using (telemetryService.TrackAsyncSqlConnectionCreationDuration())
            {
                return Task.Run(() => connectionFactory.CreateAsync()).Result;
            }
        }

        private static void ConfigureGalleryReadOnlyReplicaEntitiesContext(
            ContainerBuilder builder,
            ILoggerFactory loggerFactory,
            ConfigurationService configuration,
            ICachingSecretInjector secretInjector,
            ITelemetryService telemetryService)
        {
            var galleryDbReadOnlyReplicaConnectionFactory = CreateDbConnectionFactory(
                loggerFactory,
                nameof(ReadOnlyEntitiesContext),
                configuration.Current.SqlReadOnlyReplicaConnectionString ?? configuration.Current.SqlConnectionString,
                secretInjector);

            builder.Register(c => new ReadOnlyEntitiesContext(CreateDbConnection(galleryDbReadOnlyReplicaConnectionFactory, telemetryService)))
                .As<IReadOnlyEntitiesContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ReadOnlyEntityRepository<Package>>()
                .As<IReadOnlyEntityRepository<Package>>()
                .InstancePerLifetimeScope();
        }

        private static void ConfigureValidationEntitiesContext(
            ContainerBuilder builder,
            ILoggerFactory loggerFactory,
            ConfigurationService configuration,
            ICachingSecretInjector secretInjector,
            ITelemetryService telemetryService)
        {
            var validationDbConnectionFactory = CreateDbConnectionFactory(
                loggerFactory,
                nameof(ValidationEntitiesContext),
                configuration.Current.SqlConnectionStringValidation,
                secretInjector);

            builder.Register(c => new ValidationEntitiesContext(CreateDbConnection(validationDbConnectionFactory, telemetryService)))
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<ValidationEntityRepository<PackageValidationSet>>()
                .As<IEntityRepository<PackageValidationSet>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ValidationEntityRepository<PackageValidation>>()
                .As<IEntityRepository<PackageValidation>>()
                .InstancePerLifetimeScope();

            builder.RegisterType<ValidationEntityRepository<PackageRevalidation>>()
                .As<IEntityRepository<PackageRevalidation>>()
                .InstancePerLifetimeScope();
        }

        private void RegisterAsynchronousValidation(
            ContainerBuilder builder,
            ILoggerFactory loggerFactory,
            ConfigurationService configuration,
            ICachingSecretInjector secretInjector,
            ITelemetryService telemetryService)
        {
            builder
                .RegisterType<NuGet.Services.Validation.ServiceBusMessageSerializer>()
                .As<NuGet.Services.Validation.IServiceBusMessageSerializer>();

            // We need to setup two enqueuers for Package validation and symbol validation each publishes 
            // to a different topic for validation.
            builder
                .RegisterType<PackageValidationEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<ITopicClient>(BindingKeys.PackageValidationTopic)))
                .Keyed<IPackageValidationEnqueuer>(BindingKeys.PackageValidationEnqueuer)
                .As<IPackageValidationEnqueuer>();

            builder
                .RegisterType<PackageValidationEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<ITopicClient>(BindingKeys.SymbolsPackageValidationTopic)))
                .Keyed<IPackageValidationEnqueuer>(BindingKeys.SymbolsPackageValidationEnqueuer)
                .As<IPackageValidationEnqueuer>();

            if (configuration.Current.AsynchronousPackageValidationEnabled)
            {
                ConfigureValidationEntitiesContext(builder, loggerFactory, configuration, secretInjector, telemetryService);

                builder
                    .Register(c =>
                    {
                        return new AsynchronousPackageValidationInitiator<Package>(
                            c.ResolveKeyed<IPackageValidationEnqueuer>(BindingKeys.PackageValidationEnqueuer),
                            c.Resolve<IAppConfiguration>(),
                            c.Resolve<IDiagnosticsService>());
                    }).As<IPackageValidationInitiator<Package>>();

                builder
                    .Register(c =>
                    {
                        return new AsynchronousPackageValidationInitiator<SymbolPackage>(
                            c.ResolveKeyed<IPackageValidationEnqueuer>(BindingKeys.SymbolsPackageValidationEnqueuer),
                            c.Resolve<IAppConfiguration>(),
                            c.Resolve<IDiagnosticsService>());
                    }).As<IPackageValidationInitiator<SymbolPackage>>();

                // we retrieve the values here (on main thread) because otherwise it would run in another thread
                // and potentially cause a deadlock on async operation.
                var validationConnectionString = configuration.ServiceBus.Validation_ConnectionString;
                var validationTopicName = configuration.ServiceBus.Validation_TopicName;
                var symbolsValidationConnectionString = configuration.ServiceBus.SymbolsValidation_ConnectionString;
                var symbolsValidationTopicName = configuration.ServiceBus.SymbolsValidation_TopicName;

                builder
                    .Register(c => new TopicClientWrapper(validationConnectionString, validationTopicName, configuration.ServiceBus.ManagedIdentityClientId))
                    .As<ITopicClient>()
                    .SingleInstance()
                    .Keyed<ITopicClient>(BindingKeys.PackageValidationTopic)
                    .OnRelease(x => x.CloseAsync());

                builder
                    .Register(c => new TopicClientWrapper(symbolsValidationConnectionString, symbolsValidationTopicName, configuration.ServiceBus.ManagedIdentityClientId))
                    .As<ITopicClient>()
                    .SingleInstance()
                    .Keyed<ITopicClient>(BindingKeys.SymbolsPackageValidationTopic)
                    .OnRelease(x => x.CloseAsync());
            }
            else
            {
                // This will register all the instances of ImmediatePackageValidator<T> as IPackageValidationInitiator<T> where T is a typeof(IPackageEntity)
                builder
                    .RegisterGeneric(typeof(ImmediatePackageValidator<>))
                    .As(typeof(IPackageValidationInitiator<>));
            }

            builder.RegisterType<ValidationAdminService>()
                .AsSelf()
                .InstancePerLifetimeScope();

            builder.RegisterType<RevalidationAdminService>()
                .AsSelf()
                .InstancePerLifetimeScope();
        }

        private static List<(string name, Uri searchUri)> GetSearchClientsFromConfiguration(IGalleryConfigurationService configuration)
        {
            var searchClients = new List<(string name, Uri searchUri)>();

            if (configuration.Current.SearchServiceUriPrimary != null)
            {
                searchClients.Add((SearchClientConfiguration.SearchPrimaryInstance, configuration.Current.SearchServiceUriPrimary));
            }
            if (configuration.Current.SearchServiceUriSecondary != null)
            {
                searchClients.Add((SearchClientConfiguration.SearchSecondaryInstance, configuration.Current.SearchServiceUriSecondary));
            }

            return searchClients;
        }

        private static List<(string name, Uri searchUri)> GetPreviewSearchClientsFromConfiguration(IGalleryConfigurationService configuration)
        {
            var searchClients = new List<(string name, Uri searchUri)>();

            if (configuration.Current.PreviewSearchServiceUriPrimary != null)
            {
                searchClients.Add((SearchClientConfiguration.PreviewSearchPrimaryInstance, configuration.Current.PreviewSearchServiceUriPrimary));
            }
            if (configuration.Current.PreviewSearchServiceUriSecondary != null)
            {
                searchClients.Add((SearchClientConfiguration.PreviewSearchSecondaryInstance, configuration.Current.PreviewSearchServiceUriSecondary));
            }

            return searchClients;
        }

        private static void ConfigureSearch(
            ILoggerFactory loggerFactory,
            IGalleryConfigurationService configuration,
            ITelemetryService telemetryService,
            ServiceCollection services,
            ContainerBuilder builder)
        {
            var searchClients = GetSearchClientsFromConfiguration(configuration);

            if (searchClients.Count >= 1)
            {
                services.AddTransient<CorrelatingHttpClientHandler>();
                services.AddTransient((s) => new TracingHttpHandler(DependencyResolver.Current.GetService<IDiagnosticsService>().SafeGetSource("ExternalSearchService")));

                // Register the default search service implementation and its dependencies.
                RegisterSearchService(
                    loggerFactory,
                    configuration,
                    telemetryService,
                    services,
                    builder,
                    searchClients);

                // Register the preview search service and its dependencies with a binding key.
                var previewSearchClients = GetPreviewSearchClientsFromConfiguration(configuration);
                RegisterSearchService(
                    loggerFactory,
                    configuration,
                    telemetryService,
                    services,
                    builder,
                    previewSearchClients,
                    BindingKeys.PreviewSearchClient);
            }
            else
            {
                builder.RegisterType<LuceneSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .Keyed<ISearchService>(BindingKeys.PreviewSearchClient)
                    .InstancePerLifetimeScope();
                builder.RegisterType<LuceneIndexingService>()
                    .AsSelf()
                    .As<IIndexingService>()
                    .As<IIndexingJobFactory>()
                    .InstancePerLifetimeScope();
                builder.RegisterType<LuceneDocumentFactory>()
                    .As<ILuceneDocumentFactory>()
                    .InstancePerLifetimeScope();
            }

            builder
                .Register(c => new SearchSideBySideService(
                    c.Resolve<ISearchService>(),
                    c.ResolveKeyed<ISearchService>(BindingKeys.PreviewSearchClient),
                    c.Resolve<ITelemetryService>(),
                    c.Resolve<IMessageService>(),
                    c.Resolve<IMessageServiceConfiguration>(),
                    c.Resolve<IIconUrlProvider>(),
                    c.Resolve<IPackageFrameworkCompatibilityFactory>(),
                    c.Resolve<IFeatureFlagService>()))
                .As<ISearchSideBySideService>()
                .InstancePerLifetimeScope();

            builder
                .Register(c => new HijackSearchServiceFactory(
                    c.Resolve<HttpContextBase>(),
                    c.Resolve<IFeatureFlagService>(),
                    c.Resolve<IContentObjectService>(),
                    c.Resolve<ISearchService>(),
                    c.ResolveKeyed<ISearchService>(BindingKeys.PreviewSearchClient)))
                .As<IHijackSearchServiceFactory>()
                .InstancePerLifetimeScope();

            builder
                .Register(c => new SearchServiceFactory(
                    c.Resolve<ISearchService>(),
                    c.ResolveKeyed<ISearchService>(BindingKeys.PreviewSearchClient)))
                .As<ISearchServiceFactory>()
                .InstancePerLifetimeScope();
        }

        private static void RegisterSearchService(
            ILoggerFactory loggerFactory,
            IGalleryConfigurationService configuration,
            ITelemetryService telemetryService,
            ServiceCollection services,
            ContainerBuilder builder,
            List<(string name, Uri searchUri)> searchClients,
            string bindingKey = null)
        {
            var logger = loggerFactory.CreateLogger<SearchClientPolicies>();

            foreach (var searchClient in searchClients)
            {
                // The policy handlers will be applied from the bottom to the top.
                // The most inner one is the one added last.
                services
                    .AddHttpClient<IHttpClientWrapper, HttpClientWrapper>(
                        searchClient.name,
                        c =>
                        {
                            c.BaseAddress = searchClient.searchUri;

                            // Here we calculate a timeout for HttpClient that allows for all of the retries to occur.
                            // This  is not strictly necessary today since the timeout exception is not a case that is
                            // retried but it's best to set this right now instead of the default (100) or something
                            // too small. The timeout on HttpClient is not really used. Instead, we depend on a timeout
                            // policy from Polly.
                            var perRetryMs = configuration.Current.SearchHttpRequestTimeoutInMilliseconds;
                            var betweenRetryMs = configuration.Current.SearchCircuitBreakerWaitAndRetryIntervalInMilliseconds;
                            var maxAttempts = configuration.Current.SearchCircuitBreakerWaitAndRetryCount + 1;
                            var maxMs = (maxAttempts * perRetryMs) + ((maxAttempts - 1) * betweenRetryMs);

                            // Add another timeout on top of the theoretical max to account for CPU time.
                            maxMs += perRetryMs;

                            c.Timeout = TimeSpan.FromMilliseconds(maxMs);
                        })
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler() { AllowAutoRedirect = true })
                    .AddHttpMessageHandler<CorrelatingHttpClientHandler>()
                    .AddSearchPolicyHandlers(
                        logger,
                        searchClient.name,
                        telemetryService,
                        configuration.Current);
            }

            var registrationBuilder = builder
                .Register(c =>
                {
                    var httpClientFactory = c.Resolve<IHttpClientFactory>();
                    var httpClientWrapperFactory = c.Resolve<ITypedHttpClientFactory<HttpClientWrapper>>();
                    var httpClientWrappers = new List<IHttpClientWrapper>(searchClients.Count);
                    foreach (var searchClient in searchClients)
                    {
                        var httpClient = httpClientFactory.CreateClient(searchClient.name);
                        var httpClientWrapper = httpClientWrapperFactory.CreateClient(httpClient);
                        httpClientWrappers.Add(httpClientWrapper);
                    }

                    return new ResilientSearchHttpClient(
                        httpClientWrappers,
                        c.Resolve<ITelemetryService>());
                });

            if (bindingKey != null)
            {
                registrationBuilder
                    .Named<IResilientSearchClient>(bindingKey)
                    .InstancePerLifetimeScope();

                builder
                    .RegisterType<GallerySearchClient>()
                    .WithParameter(new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(IResilientSearchClient),
                        (pi, ctx) => ctx.ResolveKeyed<IResilientSearchClient>(bindingKey)))
                    .Named<ISearchClient>(bindingKey)
                    .InstancePerLifetimeScope();

                builder.RegisterType<ExternalSearchService>()
                    .WithParameter(new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ISearchClient),
                        (pi, ctx) => ctx.ResolveKeyed<ISearchClient>(bindingKey)))
                    .Named<ISearchService>(bindingKey)
                    .InstancePerLifetimeScope();
            }
            else
            {
                registrationBuilder
                    .As<IResilientSearchClient>()
                    .InstancePerLifetimeScope();

                builder
                    .RegisterType<GallerySearchClient>()
                    .As<ISearchClient>()
                    .InstancePerLifetimeScope();

                builder.RegisterType<ExternalSearchService>()
                    .AsSelf()
                    .As<ISearchService>()
                    .As<IIndexingService>()
                    .As<IIndexingJobFactory>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureAutocomplete(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            if (configuration.Current.SearchServiceUriPrimary != null || configuration.Current.SearchServiceUriSecondary != null)
            {
                builder.RegisterType<AutocompleteServicePackageIdsQuery>()
                    .AsSelf()
                    .As<IAutocompletePackageIdsQuery>()
                    .SingleInstance();

                builder.RegisterType<AutocompleteServicePackageVersionsQuery>()
                    .AsSelf()
                    .As<IAutocompletePackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<AutocompleteDatabasePackageIdsQuery>()
                    .AsSelf()
                    .As<IAutocompletePackageIdsQuery>()
                    .InstancePerLifetimeScope();

                builder.RegisterType<AutocompleteDatabasePackageVersionsQuery>()
                    .AsSelf()
                    .As<IAutocompletePackageVersionsQuery>()
                    .InstancePerLifetimeScope();
            }
        }

        private static void ConfigureForLocalFileSystem(ContainerBuilder builder, IGalleryConfigurationService configuration)
        {
            builder.RegisterType<FileSystemService>()
                .AsSelf()
                .As<IFileSystemService>()
                .SingleInstance();

            builder.RegisterType<FileSystemFileStorageService>()
                .AsSelf()
                .As<IFileStorageService>()
                .As<ICoreFileStorageService>()
                .SingleInstance();

            foreach (var dependent in StorageDependent.GetAll(configuration.Current))
            {
                var registration = builder.RegisterType(dependent.ImplementationType)
                    .AsSelf()
                    .As(dependent.InterfaceType);

                if (dependent.IsSingleInstance)
                {
                    registration.SingleInstance();
                }
                else
                {
                    registration.InstancePerLifetimeScope();
                }
            }

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

            builder.RegisterType<GalleryContentFileMetadataService>()
                .As<IContentFileMetadataService>()
                .SingleInstance();
        }

        private static IAuditingService GetAuditingServiceForLocalFileSystem(IAppConfiguration configuration)
        {
            var auditingPath = Path.Combine(
                FileSystemFileStorageService.ResolvePath(configuration.FileStorageDirectory),
                FileSystemAuditingService.DefaultContainerName);

            return new FileSystemAuditingService(auditingPath, AuditActor.GetAspNetOnBehalfOfAsync);
        }

        private static void ConfigureForAzureStorage(ContainerBuilder builder, IGalleryConfigurationService configuration, ITelemetryService telemetryService)
        {
            /// The goal here is to initialize a <see cref="ICloudBlobClient"/> and <see cref="IFileStorageService"/>
            /// instance for each unique connection string. Each dependent of <see cref="IFileStorageService"/> (that
            /// is, each service that has a <see cref="IFileStorageService"/> constructor parameter) is registered in
            /// <see cref="StorageDependent.GetAll(IAppConfiguration)"/> and is grouped by the respective storage
            /// connection string. Each group is given a binding key which refers to the appropriate instance of the
            /// <see cref="IFileStorageService"/>.
            var completedBindingKeys = new HashSet<string>();
            foreach (var dependent in StorageDependent.GetAll(configuration.Current))
            {
                if (completedBindingKeys.Add(dependent.BindingKey))
                {
                    builder.RegisterInstance(new CloudBlobClientWrapper(dependent.AzureStorageConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                       .AsSelf()
                       .As<ICloudBlobClient>()
                       .SingleInstance()
                       .Keyed<ICloudBlobClient>(dependent.BindingKey);

                    // Do not register the service as ICloudStorageStatusDependency because
                    // the CloudAuditingService registers it and the gallery uses the same storage account for all the containers.
                    builder.RegisterType<CloudBlobFileStorageService>()
                        .WithParameter(new ResolvedParameter(
                           (pi, ctx) => pi.ParameterType == typeof(ICloudBlobClient),
                           (pi, ctx) => ctx.ResolveKeyed<ICloudBlobClient>(dependent.BindingKey)))
                        .AsSelf()
                        .As<IFileStorageService>()
                        .As<ICoreFileStorageService>()
                        .SingleInstance()
                        .Keyed<IFileStorageService>(dependent.BindingKey);
                }

                var registration = builder.RegisterType(dependent.ImplementationType)
                    .WithParameter(new ResolvedParameter(
                       (pi, ctx) => pi.ParameterType.IsAssignableFrom(typeof(IFileStorageService)),
                       (pi, ctx) => ctx.ResolveKeyed<IFileStorageService>(dependent.BindingKey)))
                    .AsSelf()
                    .As(dependent.InterfaceType);

                if (dependent.IsSingleInstance)
                {
                    registration.SingleInstance();
                }
                else
                {
                    registration.InstancePerLifetimeScope();
                }
            }

            RegisterStatisticsServices(builder, configuration);

            builder.RegisterType<FlatContainerContentFileMetadataService>()
                .As<IContentFileMetadataService>()
                .SingleInstance();
        }

        private static IAuditingService CombineAuditingServices(IEnumerable<IAuditingService> services)
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

        private static IEnumerable<T> GetAddInServices<T>()
        {
            return GetAddInServices<T>(sp => { });
        }

        private static IEnumerable<T> GetAddInServices<T>(Action<RuntimeServiceProvider> populateProvider)
        {
            var addInsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "add-ins");

            using (var serviceProvider = RuntimeServiceProvider.Create(addInsDirectoryPath))
            {
                populateProvider(serviceProvider);
                return serviceProvider.GetExportedValues<T>();
            }
        }

        private static void RegisterAuditingServices(ContainerBuilder builder, string storageType)
        {
            if (storageType == StorageType.AzureStorage)
            {
                builder.Register(c =>
                    {
                        var configuration = c.Resolve<IAppConfiguration>();
                        return new CloudBlobClientWrapper(configuration.AzureStorage_Auditing_ConnectionString, configuration.AzureStorageReadAccessGeoRedundant);
                    })
                    .SingleInstance()
                    .Keyed<ICloudBlobClient>(BindingKeys.AuditKey);

                builder.Register(c =>
                    {
                        var blobClientFactory = c.ResolveKeyed<Func<ICloudBlobClient>>(BindingKeys.AuditKey);
                        return new CloudAuditingService(blobClientFactory, AuditActor.GetAspNetOnBehalfOfAsync);
                    })
                    .SingleInstance()
                    .AsSelf()
                    .As<ICloudStorageStatusDependency>();
            }

            builder.Register(c =>
                {
                    var configuration = c.Resolve<IAppConfiguration>();
                    IAuditingService defaultAuditingService = null;
                    switch (storageType)
                    {
                        case StorageType.FileSystem:
                        case StorageType.NotSpecified:
                            defaultAuditingService = GetAuditingServiceForLocalFileSystem(configuration);
                            break;

                        case StorageType.AzureStorage:
                            defaultAuditingService = c.Resolve<CloudAuditingService>();
                            break;
                    }

                    var auditingServices = GetAddInServices<IAuditingService>();
                    var services = new List<IAuditingService>(auditingServices);

                    if (defaultAuditingService != null)
                    {
                        services.Add(defaultAuditingService);
                    }

                    return CombineAuditingServices(services);
                })
                .AsSelf()
                .As<IAuditingService>()
                .SingleInstance();
        }

        private static void RegisterCookieComplianceService(ConfigurationService configuration, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger(nameof(CookieComplianceService));

            ICookieComplianceService service = null;
            if (configuration.Current.IsHosted)
            {
                var siteName = configuration.GetSiteRoot(true);
                service = GetAddInServices<ICookieComplianceService>(sp =>
                {
                    sp.ComposeExportedValue<ILogger>(logger);
                }).FirstOrDefault();
            }

            CookieComplianceService.Initialize(service ?? new NullCookieComplianceService(), logger);
        }

        private static void RegisterTyposquattingServiceHelper(ContainerBuilder builder, ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger(nameof(ITyposquattingServiceHelper));

            builder.Register(c =>
            {
                var typosquattingService = GetAddInServices<ITyposquattingServiceHelper>(sp =>
                {
                    sp.ComposeExportedValue<ILogger>(logger);
                }).FirstOrDefault();

                if (typosquattingService == null)
                {
                    typosquattingService = new ExactMatchTyposquattingServiceHelper();
                    logger.LogInformation("No typosquatting service helper was found, using ExactMatchTyposquattingServiceHelper instead.");
                }
                else
                {
                    logger.LogInformation("ITyposquattingServiceHelper found.");
                }

                return typosquattingService;
            })
            .As<ITyposquattingServiceHelper>()
            .SingleInstance();
        }
    }
}