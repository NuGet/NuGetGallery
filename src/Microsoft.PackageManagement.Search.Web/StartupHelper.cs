// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace Microsoft.PackageManagement.Search.Web
{
    public class StartupHelper
    {
        public const string EnvironmentVariablePrefix = "APPSETTING_";

        public static RefreshableConfiguration GetSecretInjectedConfiguration(IConfigurationRoot uninjectedConfiguration)
        {
            // Initialize KeyVault integration.
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var refreshSecretReaderSettings = new RefreshableSecretReaderSettings();
            var refreshingSecretReaderFactory = new RefreshableSecretReaderFactory(secretReaderFactory, refreshSecretReaderSettings);
            var secretReader = refreshingSecretReaderFactory.CreateSecretReader();
            var secretInjector = refreshingSecretReaderFactory.CreateSecretInjector(secretReader);

            // Attempt to inject secrets into all of the configuration strings.
            foreach (var pair in uninjectedConfiguration.AsEnumerable())
            {
                if (!string.IsNullOrWhiteSpace(pair.Value))
                {
                    // We can synchronously wait here because we are outside of the request context. It's not great
                    // but we need to fetch the initial secrets for the cache before activating any controllers or
                    // asking DI for configuration.
                    secretInjector.InjectAsync(pair.Value).Wait();
                }
            }

            // Reload the configuration with secret injection enabled. This is used by the application.
            var injectedBuilder = new ConfigurationBuilder()
                .AddInjectedJsonFile("appsettings.json", secretInjector)
                .AddInjectedJsonFile("appsettings.Development.json", secretInjector)
                .AddInjectedEnvironmentVariables(EnvironmentVariablePrefix, secretInjector);
            var injectedConfiguration = injectedBuilder.Build();

            // Now disable all secrets loads from a non-refresh path. Refresh will be called periodically from a
            // background thread. Foreground (request) threads MUST use the cache otherwise there will be a deadlock.
            refreshSecretReaderSettings.BlockUncachedReads = true;

            return new RefreshableConfiguration
            {
                SecretReaderFactory = refreshingSecretReaderFactory,
                Root = injectedConfiguration,
            };
        }

        public static void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            Action<CorsPolicyBuilder> configureCors = null)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            if (configureCors != null)
            {
                app.UseCors(configureCors);
            }

            app.UseHsts();

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static string GetControllerName<T>() where T : ControllerBase
        {
            const string ControllerSuffix = "Controller";
            var typeName = typeof(T).Name;
            if (typeName.EndsWith(ControllerSuffix, StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - ControllerSuffix.Length);
            }

            throw new ArgumentException($"The controller type name must end with '{ControllerSuffix}'.");
        }

        public static string GetOperationName<T>(HttpMethod verb, string actionName) where T : ControllerBase
        {
            return $"{verb} {GetControllerName<T>()}/{actionName}";
        }
    }

    public class RefreshableConfiguration
    {
        public IRefreshableSecretReaderFactory SecretReaderFactory { get; set; }
        public IConfigurationRoot Root { get; set; }
    }
}
