// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Features;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Security;

namespace NuGetGallery.AccountDeleter
{
    public class Job : SubcriptionProcessorJob<AccountDeleteMessage>
    {
        private const string AccountDeleteConfigurationSectionName = "AccountDeleteSettings";
        private const string DebugArgumentName = "Debug";

        private bool IsDebugMode { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            IsDebugMode = jobArgsDictionary.ContainsKey(DebugArgumentName);
            base.Init(serviceContainer, jobArgsDictionary);
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<AccountDeleteConfiguration>(configurationRoot.GetSection(AccountDeleteConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<IBrokeredMessageSerializer<AccountDeleteMessage>, AccountDeleteMessageSerializer>();
            services.AddTransient<IMessageHandler<AccountDeleteMessage>, AccountDeleteMessageHandler>();

            services.AddTransient<IAccountManager, GalleryAccountManager>();

            services.AddTransient<IAccountDeleteTelemetryService, AccountDeleteTelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, AccountDeleteTelemetryService>();

            services.AddScoped<AggregateEvaluator>();
            services.AddScoped<AlwaysRejectEvaluator>();
            services.AddScoped<AlwaysAllowEvaluator>();
            services.AddScoped<UserPackageEvaluator>();
            services.AddScoped<AccountConfirmedEvaluator>();

            services.AddScoped<IUserEvaluator>(sp =>
            {
                var evaluator = sp.GetRequiredService<AggregateEvaluator>();

                if (IsDebugMode)
                {
                    var alwaysReject = sp.GetRequiredService<AlwaysRejectEvaluator>();
                    var alwaysAllow = sp.GetRequiredService<AlwaysAllowEvaluator>();

                    evaluator.AddEvaluator(alwaysReject);
                    evaluator.AddEvaluator(alwaysAllow);
                }
                else
                {
                    // Configure evaluators here.
                    var accountConfirmedEvaluator = sp.GetRequiredService<AccountConfirmedEvaluator>();
                    evaluator.AddEvaluator(accountConfirmedEvaluator);
                }

                return evaluator;
            });

            if (IsDebugMode)
            {
                services.AddTransient<IMessageService, DebugMessageService>();
            }
            else
            {
                services.AddTransient<IMessageService, AsynchronousEmailMessageService>();
                services.AddTransient<IEmailMessageEnqueuer, EmailMessageEnqueuer>();
                services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
                services.AddTransient<ITopicClient>(serviceProvider =>
                {
                    var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<AccountDeleteConfiguration>>().Value;
                    var emailServiceBusConfiguration = configuration.EmailConfiguration.ServiceBus;
                    return new TopicClientWrapper(emailServiceBusConfiguration.ConnectionString, emailServiceBusConfiguration.TopicPath);
                });
                services.AddTransient<IMessageServiceConfiguration, CoreMessageServiceConfiguration>();
            }


            services.AddScoped<IEmailBuilderFactory, EmailBuilderFactory>();
            services.AddScoped<ITelemetryClient, TelemetryClientWrapper>();

            ConfigureGalleryServices(services, configurationRoot);
        }

        protected void ConfigureGalleryServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            if (IsDebugMode)
            {
                services.AddScoped<IDeleteAccountService, EmptyDeleteAccountService>();
                services.AddScoped<IUserService, EmptyUserService>();
            }
            else
            {
                services.AddScoped<IEntitiesContext>(sp =>
                {
                    var connectionFactory = sp.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>();
                    var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                    return new EntitiesContext(connection, readOnly: false);
                });
                services.AddScoped<IDeleteAccountService, DeleteAccountService>();

                services.AddScoped<IUserService, AccountDeleteUserService>();
                services.AddScoped<ITelemetryClient>(sp => { return TelemetryClientWrapper.Instance; });
                services.AddScoped<IDiagnosticsService, DiagnosticsService>();

                services.AddScoped<IPackageService, PackageService>();
                services.AddScoped<IPackageUpdateService, PackageUpdateService>();

                services.AddScoped<IAuthenticationService, AuthenticationService>();
                services.AddScoped<ISupportRequestService, ISupportRequestService>();

                services.AddScoped<IEditableFeatureFlagStorageService, FeatureFlagFileStorageService>();
                services.AddScoped<ICoreFileStorageService, CloudBlobFileStorageService>();
                services.AddScoped<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();

                services.AddScoped<IIndexingService, EmptyIndexingService>();
                services.AddScoped<ICredentialBuilder, CredentialBuilder>();
                services.AddScoped<ICredentialValidator, CredentialValidator>();
                services.AddScoped<IDateTimeProvider, DateTimeProvider>();
                services.AddScoped<IContentObjectService, ContentObjectService>();
                services.AddScoped<IContentService, ContentService>();
                services.AddScoped<IFileStorageService, CloudBlobFileStorageService>();
                services.AddScoped<ISourceDestinationRedirectPolicy, NoLessSecureDestinationRedirectPolicy>();

                services.AddScoped<ISupportRequestService, SupportRequestService>();
                services.AddScoped<ISupportRequestDbContext>(sp =>
                {
                    var connectionFactory = sp.GetRequiredService<ISqlConnectionFactory<SupportRequestDbConfiguration>>();
                    var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                    return new SupportRequestDbContext(connection);
                });

                services.AddScoped<ICloudBlobClient>(sp =>
                {
                    var options = sp.GetRequiredService<IOptionsSnapshot<AccountDeleteConfiguration>>();
                    var optionsSnapshot = options.Value;

                    return new CloudBlobClientWrapper(optionsSnapshot.GalleryStorageConnectionString, readAccessGeoRedundant: true);
                });

                services.AddScoped<ITelemetryService, TelemetryService>();
                services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
                services.AddScoped<IAppConfiguration, GalleryConfiguration>();
                services.AddScoped<IPackageOwnershipManagementService, PackageOwnershipManagementService>();
                services.AddScoped<IPackageOwnerRequestService, PackageOwnerRequestService>();
                services.AddScoped<IReservedNamespaceService, ReservedNamespaceService>();
                services.AddScoped<MicrosoftTeamSubscription>();

                RegisterAuditingServices(services);
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);
            containerBuilder.RegisterType<EntityRepository<User>>()
                .AsSelf()
                .As<IEntityRepository<User>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<Credential>>()
                .AsSelf()
                .As<IEntityRepository<Credential>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<Organization>>()
                .AsSelf()
                .As<IEntityRepository<Organization>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<Package>>()
                .AsSelf()
                .As<IEntityRepository<Package>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<PackageRegistration>>()
                .AsSelf()
                .As<IEntityRepository<PackageRegistration>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<Certificate>>()
                .AsSelf()
                .As<IEntityRepository<Certificate>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<ReservedNamespace>>()
                .AsSelf()
                .As<IEntityRepository<ReservedNamespace>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<AccountDelete>>()
                .AsSelf()
                .As<IEntityRepository<AccountDelete>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<PackageDelete>>()
                .AsSelf()
                .As<IEntityRepository<PackageDelete>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<PackageDeprecation>>()
                .AsSelf()
                .As<IEntityRepository<PackageDeprecation>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<Scope>>()
                .AsSelf()
                .As<IEntityRepository<Scope>>()
                .InstancePerLifetimeScope();

            containerBuilder.RegisterType<EntityRepository<PackageOwnerRequest>>()
                .AsSelf()
                .As<IEntityRepository<PackageOwnerRequest>>()
                .InstancePerLifetimeScope();
        }

        private void RegisterAuditingServices(IServiceCollection services)
        {
            if (IsDebugMode)
            {
                services.AddSingleton<AuditingService>(sp =>
                {
                    return GetAuditingServiceForLocalFileSystem();
                });
            }

            services.AddSingleton<IAuditingService>(sp =>
            {
                var addInAuditingServices = GetAddInServices<IAuditingService>();
                var auditingServices = new List<IAuditingService>(addInAuditingServices);

                try
                {
                    auditingServices.Add(sp.GetRequiredService<AuditingService>());
                }
                catch
                {
                    // no default auditing service was registered, no-op
                }

                return CombineAuditingServices(auditingServices);
            });
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

        private AuditingService GetAuditingServiceForLocalFileSystem()
        {
            var auditingPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                FileSystemAuditingService.DefaultContainerName);

            return new FileSystemAuditingService(auditingPath, AuditActor.GetAspNetOnBehalfOfAsync);
        }

        private static IEnumerable<T> GetAddInServices<T>()
        {
            var addInsDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "add-ins");

            using (var serviceProvider = RuntimeServiceProvider.Create(addInsDirectoryPath))
            {
                return serviceProvider.GetExportedValues<T>();
            }
        }
    }
}
