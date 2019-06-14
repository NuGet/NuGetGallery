// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Features;
using NuGetGallery.Security;

namespace NuGetGallery.AccountDeleter
{
    public class Job : SubcriptionProcessorJob<AccountDeleteMessage>
    {
        private const string AccountDeleteConfigurationSectionName = "AccountDeleteSettings";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<AccountDeleteConfiguration>(configurationRoot.GetSection(AccountDeleteConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<IBrokeredMessageSerializer<AccountDeleteMessage>, AccoundDeleteMessageSerializer>();
            services.AddTransient<IMessageHandler<AccountDeleteMessage>, AccountDeleteMessageHandler>();

            services.AddTransient<IAccountManager, GalleryAccountManager>();

            services.AddTransient<IAccountDeleteTelemetryService, AccountDeleteTelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, AccountDeleteTelemetryService>();
            services.AddTransient<IMessageService, EmptyMessenger>();
            services.AddSingleton<IUserEvaluator>(sp =>
            {
                var telemetry = sp.GetRequiredService<IAccountDeleteTelemetryService>();
                var aeLogger = sp.GetRequiredService<ILogger<AggregateEvaluator>>();
                var areLogger = sp.GetRequiredService<ILogger<AlwayRejectEvaluator>>();
                var evaluator = new AggregateEvaluator(telemetry, aeLogger);

                // we can configure evaluators here.
                var alwaysReject = new AlwayRejectEvaluator(areLogger);

                evaluator.AddEvaluator(alwaysReject);

                return evaluator;
            });

            services.AddSingleton<IEmailBuilderFactory, EmailBuilderFactory>();

            services.AddSingleton(new TelemetryClient());

            ConfigureGalleryServices(services, configurationRoot);
        }

        protected void ConfigureGalleryServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddSingleton<IDeleteAccountService, EmptyDeleteAccountService>();
            services.AddSingleton<IUserService, EmptyUserService>();
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
