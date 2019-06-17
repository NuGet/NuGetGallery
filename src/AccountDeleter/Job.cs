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
using NuGetGallery.AccountDeleter.Configuration;

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

            services.AddTransient<IBrokeredMessageSerializer<AccountDeleteMessage>, AccoundDeleteMessageSerializer>();
            services.AddTransient<IMessageHandler<AccountDeleteMessage>, AccountDeleteMessageHandler>();

            services.AddTransient<IAccountManager, GalleryAccountManager>();

            services.AddTransient<IAccountDeleteTelemetryService, AccountDeleteTelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, AccountDeleteTelemetryService>();
            services.AddSingleton<IUserEvaluator>(sp =>
            {
                var telemetry = sp.GetRequiredService<IAccountDeleteTelemetryService>();
                var aeLogger = sp.GetRequiredService<ILogger<AggregateEvaluator>>();
                var areLogger = sp.GetRequiredService<ILogger<AlwaysRejectEvaluator>>();
                var aaeLogger = sp.GetRequiredService<ILogger<AlwaysAllowEvaluator>>();
                var evaluator = new AggregateEvaluator(telemetry, aeLogger);

                // we can configure evaluators here.
                if (IsDebugMode)
                {
                    var alwaysReject = new AlwaysRejectEvaluator(areLogger);
                    var alwaysAllow = new AlwaysAllowEvaluator(aaeLogger);

                    evaluator.AddEvaluator(alwaysReject);
                    evaluator.AddEvaluator(alwaysAllow);
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


            services.AddSingleton<IEmailBuilderFactory, EmailBuilderFactory>();
            services.AddSingleton(new TelemetryClient());

            ConfigureGalleryServices(services, configurationRoot);
        }

        protected void ConfigureGalleryServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            if (IsDebugMode)
            {
                services.AddSingleton<IDeleteAccountService, EmptyDeleteAccountService>();
                services.AddSingleton<IUserService, EmptyUserService>();
            }
            else
            {
                services.AddSingleton<IDeleteAccountService, EmptyDeleteAccountService>();
                services.AddSingleton<IUserService, EmptyUserService>();
                //services.AddSingleton<IDeleteAccountService, DeleteAccountService>();
                //services.AddSingleton<IUserService, UserService>();
            }
            //services.AddSingleton<IPackageService, PackageService>();
            //services.AddSingleton<IPackageUpdateService, PackageUpdateService>();
            //services.AddSingleton<IPackageOwnershipManagementService, PackageOwnershipManagementService>();
            //services.AddSingleton<IReservedNamespaceService, ReservedNamespaceService>();
            //services.AddSingleton<ISecurityPolicyService, SecurityPolicyService>();
            ////services.AddSingleton<IAuthenticationService, AuthenticationService>(); // This guy needs to come out of gallery.
            ////services.AddSingleton<ISupportRequestService, SupportRequestService>(); // This guy too
            //services.AddSingleton<IEditableFeatureFlagStorageService, FeatureFlagFileStorageService>();
            //services.AddSingleton<IAuditingService, AuditingService>();
            //ITelemetryService telemetryService // Gallery Telemetry Service
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);
        }
    }
}
