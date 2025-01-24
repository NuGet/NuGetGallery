// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class Job : SubscriptionProcessorJob<CertificateValidationMessage>
    {
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<CertificateValidationMessage>, CertificateValidationMessageHandler>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var useStorageManagedIdentity = bool.Parse(configurationRoot[Constants.StorageUseManagedIdentityPropertyName]);
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;

                BlobServiceClientFactory targetStorageAccount;
                if (useStorageManagedIdentity)
                {
                    var managedIdentityClientId =
                        string.IsNullOrEmpty(configurationRoot[Constants.StorageManagedIdentityClientIdPropertyName]) ?
                        configurationRoot[Constants.ManagedIdentityClientIdKey] :
                        configurationRoot[Constants.StorageManagedIdentityClientIdPropertyName];
                    var storageAccountUri = AzureStorage.GetPrimaryServiceUri(config.DataStorageAccount);
                    var managedIdentityCredential = new ManagedIdentityCredential(managedIdentityClientId);
                    targetStorageAccount = new BlobServiceClientFactory(storageAccountUri, managedIdentityCredential);
                }
                else
                {
                    targetStorageAccount = new BlobServiceClientFactory(AzureStorageFactory.PrepareConnectionString(config.DataStorageAccount));
                }

                var storageFactory = new AzureStorageFactory(
                    targetStorageAccount,
                    config.ContainerName,
                    enablePublicAccess: false,
                    LoggerFactory.CreateLogger<AzureStorage>());
                var storage = storageFactory.Create();

                return new CertificateStore(storage, LoggerFactory.CreateLogger<CertificateStore>());
            });

            services.AddTransient<ICertificateVerifier, OnlineCertificateVerifier>();
            services.AddTransient<ICertificateValidationService, CertificateValidationService>();
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);
        }
    }
}
