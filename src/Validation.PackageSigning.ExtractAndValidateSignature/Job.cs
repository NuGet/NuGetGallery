// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;
using NuGet.Services.Validation.PackageSigning;
using NuGetGallery;

namespace NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature
{
    public class Job : SubcriptionProcessorJob<SignatureValidationMessage>
    {
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));

            services.AddTransient<ISubscriptionProcessor<SignatureValidationMessage>, SubscriptionProcessor<SignatureValidationMessage>>();

            services.AddTransient<IEntityRepository<Certificate>, EntityRepository<Certificate>>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;
                var targetStorageAccount = CloudStorageAccount.Parse(config.DataStorageAccount);

                var storageFactory = new AzureStorageFactory(targetStorageAccount, config.ContainerName, LoggerFactory.CreateLogger<AzureStorage>());
                var storage = storageFactory.Create();

                return new CertificateStore(storage, LoggerFactory.CreateLogger<CertificateStore>());
            });

            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<SignatureValidationMessage>, SignatureValidationMessageHandler>();
            services.AddTransient<IPackageSigningStateService, PackageSigningStateService>();
            services.AddTransient<ISignaturePartsExtractor, SignaturePartsExtractor>();

            services.AddTransient<ISignatureValidator, SignatureValidator>(p => new SignatureValidator(
                p.GetRequiredService<IPackageSigningStateService>(),
                PackageSignatureVerifierFactory.CreateMinimal(),
                PackageSignatureVerifierFactory.CreateFull(),
                p.GetRequiredService<ISignaturePartsExtractor>(),
                p.GetRequiredService<IEntityRepository<Certificate>>(),
                p.GetRequiredService<ILogger<SignatureValidator>>()));
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            const string validateSignatureBindingKey = "ValidateSignatureKey";
            var signatureValidationMessageHandlerType = typeof(IMessageHandler<SignatureValidationMessage>);

            containerBuilder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(Type),
                    (pi, ctx) => typeof(PackageSigningValidator))
                .As<IValidatorStateService>();

            containerBuilder
                .RegisterType<PackageSigningStateService>()
                .As<IPackageSigningStateService>();

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
