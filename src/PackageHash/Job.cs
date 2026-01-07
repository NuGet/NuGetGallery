// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Cursor;
using NuGet.Services.Entities;
using NuGet.Services.Storage;
using NuGetGallery;

namespace NuGet.Services.PackageHash
{
    public class Job : ValidationJobBase
    {
        private const string PackageHashConfigurationSectionName = "PackageHash";

        /// <summary>
        /// This is the bucket number (bucket index + 1) to execute work for. In other words, if you want to run this
        /// job on four different machines, you would set "-bucket-count" argument to "4" and execute this process four
        /// times, with "-bucket-number" argument set to "1", "2", "3", and "4". This would effectively distribute the
        /// work across four machines without any other coordination necessary.
        /// </summary>
        private const string BucketNumber = "bucket-number";

        /// <summary>
        /// This is the bucket count, which is the number of workers you want to be running in parallel.
        /// </summary>
        private const string BucketCount = "bucket-count";

        private int _bucketNumber = 1;
        private int _bucketCount = 1;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            if (jobArgsDictionary.TryGetValue(BucketCount, out var unparsedBucketCount))
            {
                if (int.TryParse(unparsedBucketCount, out var bucketCount))
                {
                    if (bucketCount > 0)
                    {
                        _bucketCount = bucketCount;
                    }
                    else
                    {
                        Logger.LogWarning(
                            "The bucket count must be greater than zero. Defaulting to {DefaultValue}.",
                            _bucketCount);
                    }
                }
                else
                {
                    Logger.LogWarning(
                        "Could not parse bucket count value {UnparsedBucketCount} as an integer. Defaulting to " +
                        "{DefaultValue}",
                        unparsedBucketCount,
                        _bucketCount);
                }
            }

            if (jobArgsDictionary.TryGetValue(BucketNumber, out var unparsedBucketNumber))
            {
                if (int.TryParse(unparsedBucketNumber, out var bucketNumber))
                {
                    if (bucketNumber > 0 && bucketNumber <= _bucketCount)
                    {
                        _bucketNumber = bucketNumber;
                    }
                    else
                    {
                        Logger.LogWarning(
                            "The bucket number must be greater than zero and less than or equal to the bucket count " +
                            "{BucketCount}. Defaulting to {DefaultValue}.",
                            _bucketCount,
                            _bucketNumber);
                    }
                }
                else
                {
                    Logger.LogWarning(
                        "Could not parse bucket number value {UnparsedBucketNumber} as an integer. Defaulting to " +
                        "{DefaultValue}",
                        unparsedBucketNumber,
                        _bucketNumber);
                }
            }

            if (_bucketNumber > _bucketCount)
            {
                throw new ArgumentException("The bucket number must be less or equal to the bucket count.");
            }

            base.Init(serviceContainer, jobArgsDictionary);
        }

        public override async Task Run()
        {
            var processor = _serviceProvider.GetRequiredService<IPackageHashProcessor>();

            await processor.ExecuteAsync(
                _bucketNumber,
                _bucketCount,
                CancellationToken.None);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<PackageHashConfiguration>(configurationRoot.GetSection(PackageHashConfigurationSectionName));

            services.AddTransient<IEntityRepository<Package>, EntityRepository<Package>>();
            services.AddTransient<IPackageHashCalculator, PackageHashCalculator>();
            services.AddTransient<IBatchProcessor, BatchProcessor>();
            services.AddTransient<IResultRecorder, ResultRecorder>();
            services.AddTransient<IPackageHashProcessor, PackageHashProcessor>();
            services.AddTransient(s =>
            {
                var address = new Uri("http://localhost");
                var fileStorageFactory = new FileStorageFactory(
                    address,
                    Directory.GetCurrentDirectory(),
                    s.GetRequiredService<ILogger<FileStorage>>());
                return new DurableCursor(
                    new Uri(address, $"cursor_{_bucketNumber}_of_{_bucketCount}.json"),
                    fileStorageFactory.Create(),
                    DateTimeOffset.MinValue);
            });
        }
    }
}
