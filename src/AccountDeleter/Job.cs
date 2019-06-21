// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Configuration;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Features;
using NuGetGallery.Security;
using System.Collections.Generic;
using System.ComponentModel.Design;
using NuGet.Services.Entities;
using NuGetGallery.Diagnostics;
using NuGet.Services.Logging;
using System.Web.Hosting;
using NuGetGallery.Configuration;
using System.Runtime.Remoting.Messaging;
using NuGetGallery.Areas.Admin.Models;
using NuGet.Jobs;

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
            services.AddTransient<AlwaysRejectEvaluator>();
            services.AddTransient<AlwaysAllowEvaluator>();
            services.AddTransient<UserPackageEvaluator>();

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
                    var alwaysReject = sp.GetRequiredService<AlwaysRejectEvaluator>();
                    evaluator.AddEvaluator(alwaysReject); // For phase 1, we never want to actually delete, so we will add this here to guarantee that

                    var userPackageEvaluator = sp.GetRequiredService<UserPackageEvaluator>();
                    evaluator.AddEvaluator(userPackageEvaluator);
                }

                return evaluator;
            });

            if (IsDebugMode)
            {
                services.AddTransient<IMessageService, EmptyMessenger>();
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
                services.AddScoped<IDeleteAccountService, EmptyDeleteAccountService>();
                //services.AddScoped<IDeleteAccountService, DeleteAccountService>();

                services.AddScoped<IUserService, MicroUserService>();
                services.AddScoped<IDiagnosticsService, LoggerDiagnosticsService>();

                services.AddScoped<IPackageService, PackageService>();

                services.AddScoped<ITelemetryService, GalleryTelemetryService>();
                services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
                services.AddScoped<IAuditingService>(sp => { return AuditingService.None; }); //Replace with real when we start doing deletes. For now, we are a readonly operation.
                services.AddScoped<IAppConfiguration, GalleryConfiguration>();
                services.AddScoped<IPackageOwnershipManagementService, PackageOwnershipManagementService>();
                services.AddScoped<IPackageOwnerRequestService, PackageOwnerRequestService>();
                services.AddScoped<IReservedNamespaceService, ReservedNamespaceService>();
                services.AddScoped<MicrosoftTeamSubscription>();
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
        }
    }
}
