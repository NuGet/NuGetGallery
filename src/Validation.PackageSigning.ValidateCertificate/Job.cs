// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
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
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class Job : SubscriptionProcessorJob<CertificateValidationMessage>
    {
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";

        private const string StorageUseManagedIdentityPropertyName = "Storage_UseManagedIdentity";
        private const string StorageManagedIdentityClientIdPropertyName = "Storage_ManagedIdentityClientId";
        private const string FallbackManagedIdentityClientIdPropertyName = "ManagedIdentityClientId";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));
            services.ConfigureStorageMsi(configurationRoot);
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);

            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<CertificateValidationMessage>, CertificateValidationMessageHandler>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var useStorageManagedIdentity = bool.Parse(configurationRoot[StorageUseManagedIdentityPropertyName]);
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;

                BlobServiceClient targetStorageAccount;
                if (useStorageManagedIdentity)
                {
                    var managedIdentityClientId =
                        string.IsNullOrEmpty(configurationRoot[StorageManagedIdentityClientIdPropertyName]) ?
                        configurationRoot[FallbackManagedIdentityClientIdPropertyName] :
                        configurationRoot[StorageManagedIdentityClientIdPropertyName];
                    var storageAccountUri = GetStorageUri(config.DataStorageAccount);
                    var managedIdentity = new ManagedIdentityCredential(managedIdentityClientId);
                    targetStorageAccount = new BlobServiceClient(storageAccountUri, managedIdentity);
                }
                else
                {
                    targetStorageAccount = new BlobServiceClient(AzureStorageFactory.PrepareConnectionString(config.DataStorageAccount));
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

        private Uri GetStorageUri(string connectionString)
        {
            // we assume that if managed Identities are being used, the connection string should be of the form:
            // BlobEndpoint=https://{storageAccount}.blob.core.windows.net"
            // This method will extract the Uri to use with BlobServiceClient

            var serviceUrl = connectionString.Split('=')[1];
            return new Uri(serviceUrl);
        }
    }
}
