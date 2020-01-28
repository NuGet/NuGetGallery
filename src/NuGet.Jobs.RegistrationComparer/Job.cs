// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;

namespace NuGet.Jobs.RegistrationComparer
{
    public class Job : JsonConfigurationJob
    {
        private const string ConfigurationSectionName = "RegistrationComparer";
        private const string RegistrationComparerMode = "compare";
        private string _mode;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            _mode = jobArgsDictionary.GetOrThrow<string>("mode");
            base.Init(serviceContainer, jobArgsDictionary);
        }

        public override async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            switch (_mode)
            {
                case RegistrationComparerMode:
                    await _serviceProvider
                        .GetRequiredService<RegistrationComparerCommand>()
                        .ExecuteAsync(CancellationToken.None);
                    break;
                default:
                    throw new InvalidOperationException("Unknown mode.");
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                   .Register(c =>
                   {
                       var options = c.Resolve<IOptionsSnapshot<RegistrationComparerConfiguration>>();
                       return CloudStorageAccount.Parse(options.Value.StorageConnectionString);
                   })
                   .AsSelf();

            containerBuilder
                .Register<IStorageFactory>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<RegistrationComparerConfiguration>>();
                    return new AzureStorageFactory(
                        c.Resolve<CloudStorageAccount>(),
                        options.Value.StorageContainer,
                        maxExecutionTime: AzureStorage.DefaultMaxExecutionTime,
                        serverTimeout: AzureStorage.DefaultServerTimeout,
                        path: string.Empty,
                        baseAddress: null,
                        useServerSideCopy: true,
                        compressContent: false,
                        verbose: true,
                        initializeContainer: false,
                        throttle: NullThrottle.Instance);
                })
                .As<IStorageFactory>();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddV3(GlobalTelemetryDimensions);

            switch (_mode)
            {
                case RegistrationComparerMode:
                    services.AddTransient<ICommitCollectorLogic, RegistrationComparerCollectorLogic>();
                    break;
            }

            services.AddTransient<RegistrationComparerCommand>();
            services.AddTransient<HiveComparer>();
            services.AddTransient<JsonComparer>();

            services.Configure<RegistrationComparerConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<CommitCollectorConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
