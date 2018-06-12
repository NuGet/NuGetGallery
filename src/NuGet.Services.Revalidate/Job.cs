// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using GalleryContext = EntitiesContext;
    using IGalleryContext = IEntitiesContext;

    public class Job : JsonConfigurationJob
    {
        private const string InitializeArgumentName = "Initialize";
        private const string VerifyInitializationArgumentName = "VerifyInitialization";
        private const string JobConfigurationSectionName = "RevalidateJob";

        private bool _initialize;
        private bool _verifyInitialization;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _initialize = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, InitializeArgumentName);
            _verifyInitialization = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, VerifyInitializationArgumentName);

            if (_initialize && !JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once))
            {
                throw new Exception($"Argument {JobArgumentNames.Once} is required if argument {InitializeArgumentName} is present.");
            }

            if (_verifyInitialization && !JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, JobArgumentNames.Once))
            {
                throw new Exception($"Argument {JobArgumentNames.Once} is required if argument {VerifyInitializationArgumentName} is present.");
            }
        }

        public override async Task Run()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                if (_initialize || _verifyInitialization)
                {
                    var initializer = scope.ServiceProvider.GetRequiredService<InitializationManager>();

                    if (_initialize)
                    {
                        Logger.LogInformation("Initializing Revalidate job...");

                        await initializer.InitializeAsync();

                        Logger.LogInformation("Revalidate job initialized");
                    }

                    if (_verifyInitialization)
                    {
                        Logger.LogInformation("Verifying initialization...");

                        await initializer.VerifyInitializationAsync();

                        Logger.LogInformation("Initialization verified");
                    }
                }
                else
                {
                    // TODO: https://github.com/NuGet/Engineering/issues/1443
                    // Send revalidation requests to the Orchestrator.
                    throw new NotImplementedException();
                }
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(JobConfigurationSectionName));
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.Initialization);

            services.AddScoped<IGalleryContext>(provider =>
            {
                var config = provider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value;

                return new GalleryContext(config.ConnectionString, readOnly: false);
            });

            services.AddScoped<IRevalidationStateService, RevalidationStateService>();
            services.AddScoped<IPackageFinder, PackageFinder>();
            services.AddScoped<InitializationManager>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}