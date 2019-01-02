// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Integration.WebApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.SearchService.Controllers;

namespace NuGet.Services.SearchService
{
    public static class WebApiConfig
    {
        private const string ControllerSuffix = "Controller";
        private const string ConfigurationSectionName = "SearchService";
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        public static void Register(HttpConfiguration config)
        {
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new StringEnumConverter());

            var dependencyResolver = GetDependencyResolver(config);
            config.DependencyResolver = dependencyResolver;

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "V2Search",
                routeTemplate: "search/query",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.V2SearchAsync),
                });

            config.Routes.MapHttpRoute(
                name: "V3Search",
                routeTemplate: "query",
                defaults: new
                {
                    controller = GetControllerName<SearchController>(),
                    action = nameof(SearchController.V3SearchAsync),
                });

            config.EnsureInitialized();

            HostingEnvironment.QueueBackgroundWorkItem(token => ReloadAuxiliaryFilesAsync(dependencyResolver, token));
        }

        private static async Task ReloadAuxiliaryFilesAsync(IDependencyResolver dependencyResolver, CancellationToken token)
        {
            var loader = (IAuxiliaryFileReloader)dependencyResolver.GetService(typeof(IAuxiliaryFileReloader));
            await loader.ReloadContinuouslyAsync(token);
        }

        private static IDependencyResolver GetDependencyResolver(HttpConfiguration config)
        {
            var configurationRoot = GetConfigurationRoot(@"Settings\dev.json", out var secretInjector);

            var services = new ServiceCollection();
            services.AddSingleton(secretInjector);
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.Configure<AzureSearchConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<SearchServiceConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.AddLogging();
            services.AddAzureSearch();

            var builder = new ContainerBuilder();
            builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            builder.RegisterWebApiFilterProvider(config);
            builder.RegisterWebApiModelBinderProvider();
            builder.Populate(services);
            builder.AddAzureSearch();

            var container = builder.Build();
            return new AutofacWebApiDependencyResolver(container);
        }

        private static IConfigurationRoot GetConfigurationRoot(string configurationFilename, out ISecretInjector secretInjector)
        {
            var basePath = HostingEnvironment.MapPath("~/");

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private static string GetControllerName<T>() where T : ApiController
        {
            var typeName = typeof(T).Name;
            if (typeName.EndsWith(ControllerSuffix, StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - ControllerSuffix.Length);
            }

            throw new ArgumentException($"The controller type name must end with '{ControllerSuffix}'.");
        }
    }
}