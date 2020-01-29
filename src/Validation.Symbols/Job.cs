// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.ServiceBus;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace Validation.Symbols
{
    public class Job : SubcriptionProcessorJob<SymbolsValidatorMessage>
    {
        private const string SymbolsConfigurationSectionName = "SymbolsConfiguration";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<SymbolsValidatorConfiguration>(configurationRoot.GetSection(SymbolsConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();
            services.AddTransient<IBrokeredMessageSerializer<SymbolsValidatorMessage>, SymbolsValidatorMessageSerializer>();
            services.AddTransient<IMessageHandler<SymbolsValidatorMessage>, SymbolsValidatorMessageHandler>();
            services.AddTransient<ISymbolsValidatorService, SymbolsValidatorService>();
            services.AddTransient<IZipArchiveService, ZipArchiveService>();
            services.AddSingleton<ISymbolsFileService>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<SymbolsValidatorConfiguration>>();
                var packageStorageService = new CloudBlobCoreFileStorageService(
                    new CloudBlobClientWrapper(
                        configurationAccessor.Value.PackageConnectionString,
                        readAccessGeoRedundant: false),
                    c.GetRequiredService<IDiagnosticsService>(),
                    c.GetRequiredService<ICloudBlobContainerInformationProvider>());

                var packageValidationStorageService = new CloudBlobCoreFileStorageService(
                    new CloudBlobClientWrapper(
                        configurationAccessor.Value.ValidationPackageConnectionString,
                        readAccessGeoRedundant: false),
                    c.GetRequiredService<IDiagnosticsService>(),
                    c.GetRequiredService<ICloudBlobContainerInformationProvider>());

                return new SymbolsFileService(packageStorageService, packageValidationStorageService, c.GetRequiredService<IFileDownloader>());
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);

            containerBuilder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.SymbolsValidator)
                .As<IValidatorStateService>();
        }
    }
}