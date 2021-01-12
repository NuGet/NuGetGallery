// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace NuGet.Services.SearchService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(configBuilder =>
                {
                    // To minimize the number of configuration variables, we remove the default environment variable
                    // sources which pull in all environment variables available to the process. In our case, we only
                    // need environment variables prefixed with "APPSETTING_", which is what Azure App Services uses to
                    // expose settings to our running process. When developing locally, the appsettings.Development.json
                    // file can be used.
                    foreach (var source in configBuilder.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToList())
                    {
                        configBuilder.Sources.Remove(source);
                    }
                    configBuilder.AddEnvironmentVariables(Startup.EnvironmentVariablePrefix);
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
