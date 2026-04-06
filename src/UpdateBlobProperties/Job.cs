// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Autofac;
using Azure.Storage.Blobs;
using NuGet.Jobs;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.Storage;
using NuGetGallery;

namespace UpdateBlobProperties
{
    public class Job : ValidationJobBase
    {
        private const string UpdateBlobPropertiesConfigurationSectionName = "UpdateBlobProperties";

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);
        }

        public override async Task Run()
        {
            var processor = _serviceProvider.GetRequiredService<IProcessor>();

            await processor.ExecuteAsync(CancellationToken.None);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            // Update blob properties of package version index in FlatContainer
            services.AddTransient<BlobInfo, BlobInfoOfPackageVersionIndexInFlatContainer>();

            services.Configure<UpdateBlobPropertiesConfiguration>(configurationRoot.GetSection(UpdateBlobPropertiesConfigurationSectionName));
            services.AddTransient<IProcessor, Processor>();
            services.AddTransient<ICollector, Collector>();
            services.AddTransient<IEntityRepository<Package>, EntityRepository<Package>>();

            services.AddTransient(s =>
            {
                var address = new Uri("http://localhost");
                var fileStorageFactory = new FileStorageFactory(
                    address,
                    Directory.GetCurrentDirectory(),
                    s.GetRequiredService<ILogger<FileStorage>>());

                return new Cursor(new Uri(address, "cursor.json"), fileStorageFactory.Create(), defaultValue: 0);
            });
        }
    }
}
