// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs.Catalog2Registration;
using NuGet.Services.V3;

namespace NuGet.Jobs
{
    public class Job : JsonConfigurationJob
    {
        private const string ConfigurationSectionName = "Catalog2Registration";

        public override async Task Run()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.MaxServicePointIdleTime = 10000;

            await _serviceProvider.GetRequiredService<Catalog2RegistrationCommand>().ExecuteAsync();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder.AddCatalog2Registration();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddCatalog2Registration();

            services.Configure<Catalog2RegistrationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<CommitCollectorConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
        }
    }
}
