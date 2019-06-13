// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs.Validation;
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
        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<IBrokeredMessageSerializer<AccountDeleteMessage>, AccoundDeleteMessageSerializer>();
            services.AddTransient<IMessageHandler<AccountDeleteMessage>, AccountDeleteMessageHandler>();

            services.AddTransient<IAccountManager, GalleryAccountManager>();

            services.AddTransient<IAccountDeleteTelemetryService, AccountDeleteTelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, AccountDeleteTelemetryService>();
            services.AddSingleton(new TelemetryClient());
        }

        protected void ConfigureGalleryServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddSingleton<IDeleteAccountService, DeleteAccountService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IPackageService, PackageService>();
            services.AddSingleton<IPackageUpdateService, PackageUpdateService>();
            services.AddSingleton<IPackageOwnershipManagementService, PackageOwnershipManagementService>();
            services.AddSingleton<IReservedNamespaceService, ReservedNamespaceService>();
            services.AddSingleton<ISecurityPolicyService, SecurityPolicyService>();
            //services.AddSingleton<IAuthenticationService, AuthenticationService>(); // This guy needs to come out of gallery.
            //services.AddSingleton<ISupportRequestService, SupportRequestService>(); // This guy too
            services.AddSingleton<IEditableFeatureFlagStorageService, FeatureFlagFileStorageService>();
            services.AddSingleton<IAuditingService, AuditingService>();
            //ITelemetryService telemetryService // Gallery Telemetry Service
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);
        }
    }
}
