// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGet.SupportRequests.Notifications
{
    internal class Job
        : JsonConfigurationJob
    {
        private InitializationConfiguration _configuration;
        private string _taskName;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _taskName = jobArgsDictionary[JobArgumentNames.ScheduledTask];
            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;
        }

        public override async Task Run()
        {
            var scheduledTask = ScheduledTaskFactory.Create(
                _taskName,
                _configuration,
                OpenSqlConnectionAsync<SupportRequestDbConfiguration>,
                LoggerFactory);

            await scheduledTask.RunAsync();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }
    }
}