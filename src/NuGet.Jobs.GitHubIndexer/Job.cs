// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGetGallery;
using Octokit;

namespace NuGet.Jobs.GitHubIndexer
{
    public class Job : JsonConfigurationJob
    {
        private const string GitHubIndexerConfigurationSectionName = "GitHubIndexer";

        public override async Task Run()
        {
            await _serviceProvider.GetRequiredService<ReposIndexer>().RunAsync();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            var assembly = Assembly.GetEntryAssembly();
            var assemblyName = assembly.GetName().Name;
            var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

            services.AddTransient<IGitRepoSearcher, GitHubSearcher>();
            services.AddSingleton<IGitHubClient>(provider => new GitHubClient(new ProductHeaderValue(assemblyName, assemblyVersion)));
            services.AddSingleton<IGitHubSearchWrapper, GitHubSearchWrapper>();
            services.AddTransient<RepoUtils>();
            services.AddTransient<ReposIndexer>();
            services.AddTransient<IRepositoriesCache, DiskRepositoriesCache>();
            services.AddTransient<IConfigFileParser, ConfigFileParser>();
            services.AddTransient<IRepoFetcher, RepoFetcher>();
            services.AddTransient<ICloudBlobClient>(provider => {
                var config = provider.GetRequiredService<IOptionsSnapshot<GitHubIndexerConfiguration>>();
                return new CloudBlobClientWrapper(config.Value.StorageConnectionString, config.Value.StorageReadAccessGeoRedundant);
            });

            services.Configure<GitHubIndexerConfiguration>(configurationRoot.GetSection(GitHubIndexerConfigurationSectionName));
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }
    }
}