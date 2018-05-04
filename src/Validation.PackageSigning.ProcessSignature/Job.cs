// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
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

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class Job : SubcriptionProcessorJob<SignatureValidationMessage>
    {
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";
        private const string ProcessSignatureConfigurationSectionName = "ProcessSignature";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));
            services.Configure<ProcessSignatureConfiguration>(configurationRoot.GetSection(ProcessSignatureConfigurationSectionName));

            services.AddTransient<ISubscriptionProcessor<SignatureValidationMessage>, SubscriptionProcessor<SignatureValidationMessage>>();

            services.AddScoped<IEntitiesContext>(serviceProvider =>
                new EntitiesContext(
                    serviceProvider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value.ConnectionString,
                    readOnly: false));
            services.Add(ServiceDescriptor.Transient(typeof(IEntityRepository<>), typeof(EntityRepository<>)));
            services.AddTransient<ICorePackageService, CorePackageService>();

            services.AddTransient<ITelemetryService, TelemetryService>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;
                var targetStorageAccount = CloudStorageAccount.Parse(config.DataStorageAccount);

                var storageFactory = new AzureStorageFactory(targetStorageAccount, config.ContainerName, LoggerFactory.CreateLogger<AzureStorage>());
                var storage = storageFactory.Create();

                return new CertificateStore(storage, LoggerFactory.CreateLogger<CertificateStore>());
            });

            services.AddTransient<IProcessorPackageFileService, ProcessorPackageFileService>(p => new ProcessorPackageFileService(
                p.GetRequiredService<ICoreFileStorageService>(),
                typeof(PackageSigningValidator),
                p.GetRequiredService<ILogger<ProcessorPackageFileService>>()));

            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<SignatureValidationMessage>, SignatureValidationMessageHandler>();
            services.AddTransient<IPackageSigningStateService, PackageSigningStateService>();
            services.AddTransient<ISignaturePartsExtractor, SignaturePartsExtractor>();
            services.AddTransient<ISignatureFormatValidator, SignatureFormatValidator>();
            services.AddTransient<ISignatureValidator, SignatureValidator>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            const string validateSignatureBindingKey = "ValidateSignatureKey";
            var signatureValidationMessageHandlerType = typeof(IMessageHandler<SignatureValidationMessage>);

            containerBuilder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.PackageSigning)
                .As<IValidatorStateService>();

            containerBuilder
                .RegisterType<ScopedMessageHandler<SignatureValidationMessage>>()
                .Keyed<IMessageHandler<SignatureValidationMessage>>(validateSignatureBindingKey);

            containerBuilder
                .RegisterType<SubscriptionProcessor<SignatureValidationMessage>>()
                .WithParameter(
                    (parameter, context) => parameter.ParameterType == signatureValidationMessageHandlerType,
                    (parameter, context) => context.ResolveKeyed(validateSignatureBindingKey, signatureValidationMessageHandlerType))
                .As<ISubscriptionProcessor<SignatureValidationMessage>>();
        }
    }
}
