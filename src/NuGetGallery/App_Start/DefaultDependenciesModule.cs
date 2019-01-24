// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Autofac.Core;
using Elmah;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Entities;
using NuGet.Services.KeyVault;
using NuGet.Services.Licenses;
using NuGet.Services.Logging;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Search.Client;
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
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Lucene;
using NuGetGallery.Infrastructure.Mail;
using NuGetGallery.Security;
using SecretReaderFactory = NuGetGallery.Configuration.SecretReader.SecretReaderFactory;

namespace NuGetGallery
{
    public class DefaultDependenciesModule : Module
    {
        public static class BindingKeys
        {
            public const string PackageValidationTopic = "PackageValidationBindingKey";
            public const string SymbolsPackageValidationTopic = "SymbolsPackageValidationBindingKey";
            public const string PackageValidationEnqueuer = "PackageValidationEnqueuerBindingKey";
            public const string SymbolsPackageValidationEnqueuer = "SymbolsPackageValidationEnqueuerBindingKey";
            public const string EmailPublisherTopic = "EmailPublisherBindingKey";
        }

        protected override void Load(ContainerBuilder builder)
        {
            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: false);
            var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration);
            builder.RegisterInstance(loggerFactory)
                .AsSelf()
                .As<ILoggerFactory>();
            builder.RegisterGeneric(typeof(Logger<>))
                .As(typeof(ILogger<>))
                .SingleInstance();

            var telemetryClient = TelemetryClientWrapper.Instance;
            builder.RegisterInstance(telemetryClient)
                .AsSelf()
                .As<ITelemetryClient>()
                .SingleInstance();

            var diagnosticsService = new DiagnosticsService(telemetryClient);
            builder.RegisterInstance(diagnosticsService)
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();

            var configuration = new ConfigurationService();
            var secretReaderFactory = new SecretReaderFactory(configuration);
            var secretReader = secretReaderFactory.CreateSecretReader();
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReader);

            builder.RegisterInstance(secretInjector)
                .AsSelf()
                .As<ISecretInjector>()
                .SingleInstance();

            configuration.SecretInjector = secretInjector;

            UrlHelperExtensions.SetConfigurationService(configuration);

            builder.RegisterInstance(configuration)
                .AsSelf()
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

            builder.RegisterType<TelemetryService>().As<ITelemetryService>().SingleInstance();
            builder.RegisterType<CredentialBuilder>().As<ICredentialBuilder>().SingleInstance();
            builder.RegisterType<CredentialValidator>().As<ICredentialValidator>().SingleInstance();

            builder.RegisterInstance(LuceneCommon.GetDirectory(configuration.Current.LuceneIndexLocation))
                .As<Lucene.Net.Store.Directory>()
                .SingleInstance();

            ConfigureSearch(builder, configuration);

            builder.RegisterType<DateTimeProvider>().AsSelf().As<IDateTimeProvider>().SingleInstance();

            builder.RegisterType<HttpContextCacheService>()
                .AsSelf()
                .As<ICacheService>()
                .InstancePerLifetimeScope();

            var galleryDbConnectionFactory = CreateDbConnectionFactory(
                diagnosticsService,
                nameof(EntitiesContext),
                configuration.Current.SqlConnectionString,
                secretInjector);

            builder.RegisterInstance(galleryDbConnectionFactory)
                .AsSelf()
                .As<ISqlConnectionFactory>()
                .SingleInstance();

            builder.Register(c => new EntitiesContext(CreateDbConnection(galleryDbConnectionFactory), configuration.Current.ReadOnlyMode))
                .AsSelf()
                .As<IEntitiesContext>()
                .As<DbContext>()
                .InstancePerLifetimeScope();

            builder.RegisterType<EntityRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
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

            var supportDbConnectionFactory = CreateDbConnectionFactory(
                diagnosticsService,
                nameof(SupportRequestDbContext),
                configuration.Current.SqlConnectionStringSupportRequest,
                secretInjector);

            builder.Register(c => new SupportRequestDbContext(CreateDbConnection(supportDbConnectionFactory)))
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

            builder.RegisterType<PackageDeleteService>()
                .AsSelf()
                .As<IPackageDeleteService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<DeleteAccountService>()
                .AsSelf()
                .As<IDeleteAccountService>()
                .InstancePerLifetimeScope();

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

            builder.RegisterType<TyposquattingService>()
                .AsSelf()
                .As<ITyposquattingService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<TyposquattingCheckListCacheService>()
                .AsSelf()
                .As<ITyposquattingCheckListCacheService>()
                .SingleInstance();

            builder.Register<ServiceDiscoveryClient>(c =>
                    new ServiceDiscoveryClient(c.Resolve<IAppConfiguration>().ServiceDiscoveryUri))
                .As<IServiceDiscoveryClient>();

            builder.RegisterType<LicenseExpressionSplitter>()
                .As<ILicenseExpressionSplitter>()
                .InstancePerLifetimeScope();

            builder.RegisterType<LicenseExpressionParser>()
                .As<ILicenseExpressionParser>()
                .InstancePerLifetimeScope();

            builder.RegisterType<LicenseExpressionSegmentator>()
                .As<ILicenseExpressionSegmentator>()
                .InstancePerLifetimeScope();

            RegisterMessagingService(builder, configuration);

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
                    ConfigureForAzureStorage(builder, configuration, telemetryClient);
                    defaultAuditingService = GetAuditingServiceForAzureStorage(builder, configuration);
                    break;
            }

            RegisterAsynchronousValidation(builder, diagnosticsService, configuration, secretInjector);

            RegisterAuditingServices(builder, defaultAuditingService);

            RegisterCookieComplianceService(builder, configuration, diagnosticsService);

            builder.RegisterType<MicrosoftTeamSubscription>()
                .AsSelf()
                .InstancePerLifetimeScope();

            if (configuration.Current.Environment == GalleryConstants.DevelopmentEnvironment)
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
                .Register(c => new TopicClientWrapper(emailPublisherConnectionString, emailPublisherTopicName))
                .As<ITopicClient>()
                .SingleInstance()
                .Keyed<ITopicClient>(BindingKeys.EmailPublisherTopic)
                .OnRelease(x => x.Close());

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

        private static ISqlConnectionFactory CreateDbConnectionFactory(IDiagnosticsService diagnostics, string name,
            string connectionString, ISecretInjector secretInjector)
        {
            var logger = diagnostics.SafeGetSource($"AzureSqlConnectionFactory-{name}");
            return new AzureSqlConnectionFactory(connectionString, secretInjector, logger);
        }

        private static DbConnection CreateDbConnection(ISqlConnectionFactory connectionFactory)
        {
            return Task.Run(() => connectionFactory.CreateAsync()).Result;
        }

        private static void ConfigureValidationEntitiesContext(ContainerBuilder builder, IDiagnosticsService diagnostics,
            ConfigurationService configuration, ISecretInjector secretInjector)
        {
            var validationDbConnectionFactory = CreateDbConnectionFactory(
                diagnostics,
                nameof(ValidationEntitiesContext),
                configuration.Current.SqlConnectionStringValidation,
                secretInjector);

            builder.Register(c => new ValidationEntitiesContext(CreateDbConnection(validationDbConnectionFactory)))
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

        private void RegisterAsynchronousValidation(ContainerBuilder builder, IDiagnosticsService diagnostics,
            ConfigurationService configuration, ISecretInjector secretInjector)
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
                ConfigureValidationEntitiesContext(builder, diagnostics, configuration, secretInjector);

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
                    .Register(c => new TopicClientWrapper(validationConnectionString, validationTopicName))
                    .As<ITopicClient>()
                    .SingleInstance()
                    .Keyed<ITopicClient>(BindingKeys.PackageValidationTopic)
                    .OnRelease(x => x.Close());

                builder
                    .Register(c => new TopicClientWrapper(symbolsValidationConnectionString, symbolsValidationTopicName))
                    .As<ITopicClient>()
                    .SingleInstance()
                    .Keyed<ITopicClient>(BindingKeys.SymbolsPackageValidationTopic)
                    .OnRelease(x => x.Close());
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

            builder.RegisterInstance(new SqlErrorLog(configuration.Current.SqlConnectionString))
                .As<ErrorLog>()
                .SingleInstance();

            builder.RegisterType<GalleryContentFileMetadataService>()
                .As<IContentFileMetadataService>()
                .InstancePerLifetimeScope();
        }

        private static IAuditingService GetAuditingServiceForLocalFileSystem(IGalleryConfigurationService configuration)
        {
            var auditingPath = Path.Combine(
                FileSystemFileStorageService.ResolvePath(configuration.Current.FileStorageDirectory),
                FileSystemAuditingService.DefaultContainerName);

            return new FileSystemAuditingService(auditingPath, AuditActor.GetAspNetOnBehalfOfAsync);
        }

        private static void ConfigureForAzureStorage(ContainerBuilder builder, IGalleryConfigurationService configuration, ITelemetryClient telemetryClient)
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

                    builder.RegisterType<CloudBlobFileStorageService>()
                        .WithParameter(new ResolvedParameter(
                           (pi, ctx) => pi.ParameterType == typeof(ICloudBlobClient),
                           (pi, ctx) => ctx.ResolveKeyed<ICloudBlobClient>(dependent.BindingKey)))
                        .AsSelf()
                        .As<IFileStorageService>()
                        .As<ICoreFileStorageService>()
                        .As<ICloudStorageStatusDependency>()
                        .SingleInstance()
                        .Keyed<IFileStorageService>(dependent.BindingKey);
                }

                var registration = builder.RegisterType(dependent.ImplementationType)
                    .WithParameter(new ResolvedParameter(
                       (pi, ctx) => pi.ParameterType == typeof(IFileStorageService),
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

            // when running on Windows Azure, we use a back-end job to calculate stats totals and store in the blobs
            builder.RegisterInstance(new JsonAggregateStatsService(configuration.Current.AzureStorage_Statistics_ConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IAggregateStatsService>()
                .SingleInstance();

            // when running on Windows Azure, pull the statistics from the warehouse via storage
            builder.RegisterInstance(new CloudReportService(configuration.Current.AzureStorage_Statistics_ConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .AsSelf()
                .As<IReportService>()
                .As<ICloudStorageStatusDependency>()
                .SingleInstance();

            // when running on Windows Azure, download counts come from the downloads.v1.json blob
            var downloadCountService = new CloudDownloadCountService(
                telemetryClient,
                configuration.Current.AzureStorage_Statistics_ConnectionString,
                configuration.Current.AzureStorageReadAccessGeoRedundant);

            builder.RegisterInstance(downloadCountService)
                .AsSelf()
                .As<IDownloadCountService>()
                .SingleInstance();
            ObjectMaterializedInterception.AddInterceptor(new DownloadCountObjectMaterializedInterceptor(downloadCountService));

            builder.RegisterType<JsonStatisticsService>()
                .AsSelf()
                .As<IStatisticsService>()
                .SingleInstance();

            builder.RegisterInstance(new TableErrorLog(configuration.Current.AzureStorage_Errors_ConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant))
                .As<ErrorLog>()
                .SingleInstance();

            builder.RegisterType<FlatContainerContentFileMetadataService>()
                .As<IContentFileMetadataService>()
                .InstancePerLifetimeScope();
        }

        private static IAuditingService GetAuditingServiceForAzureStorage(ContainerBuilder builder, IGalleryConfigurationService configuration)
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

            var service = new CloudAuditingService(instanceId, localIp, configuration.Current.AzureStorage_Auditing_ConnectionString, configuration.Current.AzureStorageReadAccessGeoRedundant, AuditActor.GetAspNetOnBehalfOfAsync);

            builder.RegisterInstance(service)
                .As<ICloudStorageStatusDependency>()
                .SingleInstance();

            return service;
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

        private static IEnumerable<T> GetAddInServices<T>(ContainerBuilder builder)
        {
            var addInsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "add-ins");

            using (var serviceProvider = RuntimeServiceProvider.Create(addInsDirectoryPath))
            {
                return serviceProvider.GetExportedValues<T>();
            }
        }

        private static void RegisterAuditingServices(ContainerBuilder builder, IAuditingService defaultAuditingService)
        {
            var auditingServices = GetAddInServices<IAuditingService>(builder);
            var services = new List<IAuditingService>(auditingServices);

            if (defaultAuditingService != null)
            {
                services.Add(defaultAuditingService);
            }

            var service = CombineAuditingServices(services);

            builder.RegisterInstance(service)
                .AsSelf()
                .As<IAuditingService>()
                .SingleInstance();
        }

        private static void RegisterCookieComplianceService(ContainerBuilder builder, ConfigurationService configuration, DiagnosticsService diagnostics)
        {
            var service = GetAddInServices<ICookieComplianceService>(builder).FirstOrDefault() as CookieComplianceServiceBase;

            if (service == null)
            {
                service = new NullCookieComplianceService();
            }

            builder.RegisterInstance(service)
                .AsSelf()
                .As<ICookieComplianceService>()
                .SingleInstance();

            if (configuration.Current.IsHosted)
            {
                // Initialize the service on App_Start to avoid any performance degradation during initial requests.
                var siteName = configuration.GetSiteRoot(true);
                HostingEnvironment.QueueBackgroundWorkItem(async cancellationToken => await service.InitializeAsync(siteName, diagnostics, cancellationToken));
            }
        }
    }
}