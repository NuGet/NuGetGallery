// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using NuGetGallery;
using ProcessSignatureConfiguration = NuGet.Jobs.Validation.PackageSigning.Configuration.ProcessSignatureConfiguration;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class Job : SubscriptionProcessorJob<SignatureValidationMessage>
    {
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";
        private const string ProcessSignatureConfigurationSectionName = "ProcessSignature";
        private const string SasDefinitionConfigurationSectionName = "SasDefinitions";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));
            services.Configure<ProcessSignatureConfiguration>(configurationRoot.GetSection(ProcessSignatureConfigurationSectionName));
            services.Configure<SasDefinitionConfiguration>(configurationRoot.GetSection(SasDefinitionConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<ISubscriptionProcessor<SignatureValidationMessage>, SubscriptionProcessor<SignatureValidationMessage>>();

            services.AddScoped<IEntitiesContext>(p =>
            {
                return new EntitiesContext(CreateSqlConnection<GalleryDbConfiguration>(), readOnly: false);
            });

            services.Add(ServiceDescriptor.Transient(typeof(IEntityRepository<>), typeof(EntityRepository<>)));
            services.AddTransient<ICorePackageService, CorePackageService>();

            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;
                var targetStorageAccount = new BlobServiceClient(AzureStorageFactory.PrepareConnectionString(config.DataStorageAccount));

                var storageFactory = new AzureStorageFactory(
                    targetStorageAccount,
                    config.ContainerName,
                    enablePublicAccess: false,
                    LoggerFactory.CreateLogger<AzureStorage>());
                var storage = storageFactory.Create();

                return new CertificateStore(storage, LoggerFactory.CreateLogger<CertificateStore>());
            });

            services.AddTransient<IProcessorPackageFileService, ProcessorPackageFileService>(p => new ProcessorPackageFileService(
                p.GetRequiredService<ICoreFileStorageService>(),
                typeof(PackageSignatureProcessor),
                p.GetRequiredService<ISharedAccessSignatureService>(),
                p.GetRequiredService<ILogger<ProcessorPackageFileService>>()));

            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<SignatureValidationMessage>, SignatureValidationMessageHandler>();
            services.AddTransient<IPackageSigningStateService, PackageSigningStateService>();
            services.AddTransient<ISignaturePartsExtractor, SignaturePartsExtractor>();
            services.AddTransient<ISignatureFormatValidator, SignatureFormatValidator>();
            services.AddTransient<ISignatureValidator, SignatureValidator>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);

            containerBuilder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.PackageSignatureProcessor)
                .As<IValidatorStateService>();
        }
    }
}
