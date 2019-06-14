// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NuGet.Jobs.GitHubIndexer
{
    public class Job : JsonConfigurationJob
    {
        public Job()
        {
        }

        public override Task Run()
        {
            throw new System.NotImplementedException();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            throw new System.NotImplementedException();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            throw new System.NotImplementedException();
        }
    }
}