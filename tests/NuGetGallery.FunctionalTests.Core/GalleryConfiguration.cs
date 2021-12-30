// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NuGet.Services.Configuration;

namespace NuGetGallery.FunctionalTests
{
    public class GalleryConfiguration
    {
        public static GalleryConfiguration Instance;

        public string GalleryBaseUrl => "staging".Equals(Slot, StringComparison.OrdinalIgnoreCase) ? StagingBaseUrl : ProductionBaseUrl;

        public string Slot { get; set; }
        public string ProductionBaseUrl { get; set; }
        public string StagingBaseUrl { get; set; }
        public string SearchServiceBaseUrl { get; set; }
        public string EmailServerHost { get; set; }
        public bool DefaultSecurityPoliciesEnforced { get; set; }
        public bool TestPackageLock { get; set; }
        public AccountConfiguration Account { get; set; }
        public OrganizationConfiguration AdminOrganization { get; set; }
        public OrganizationConfiguration CollaboratorOrganization { get; set; }
        public BrandingConfiguration Branding { get; set; }
        public bool TyposquattingCheckAndBlockUsers { get; set; }

        static GalleryConfiguration()
        {
            try
            {
                // This test suite hits the gallery which requires TLS 1.2 (at least in some environments).
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // Please refer to https://github.com/NuGet/NuGetGallery/pull/8890 for information on why this is needed.
                RedirectAssembly("Newtonsoft.Json");

                // Load the configuration without injection. This allows us to read KeyVault configuration.
                var uninjectedBuilder = new ConfigurationBuilder()
                    .AddJsonFile(EnvironmentSettings.ConfigurationFilePath, optional: false);
                var uninjectedConfiguration = uninjectedBuilder.Build();

                // Initialize KeyVault integration.
                var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
                var secretInjector = secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader());

                // Initialize the configuration with KeyVault secrets injected.
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Environment.CurrentDirectory)
                    .AddInjectedJsonFile(EnvironmentSettings.ConfigurationFilePath, secretInjector);
                var instance = new GalleryConfiguration();
                builder.Build().Bind(instance);

                Instance = instance;
            }
            catch (ArgumentException ae)
            {
                throw new ArgumentException(
                    $"No configuration file was specified! Set the '{EnvironmentSettings.ConfigurationFilePathVariableName}' environment variable to the path to a JSON configuration file.",
                    ae);
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    $"Unable to load the JSON configuration file. " +
                    $"Make sure the JSON configuration file exists at the path specified by the '{EnvironmentSettings.ConfigurationFilePathVariableName}' " +
                    $"and that it is a valid JSON file containing all required configuration.",
                    e);
            }
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/32698357
        /// </summary>
        public static void RedirectAssembly(string shortName)
        {
            ResolveEventHandler handler = null;

            handler = (sender, args) =>
            {
                var requestedAssembly = new AssemblyName(args.Name);
                if (requestedAssembly.Name != shortName)
                {
                    return null;
                }

                var current = AppDomain
                    .CurrentDomain
                    .GetAssemblies()
                    .LastOrDefault(x => x.GetName().Name == shortName);

                return current;
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }

        public class AccountConfiguration : OrganizationConfiguration
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public string ApiKeyPush { get; set; }
            public string ApiKeyPushVersion { get; set; }
            public string ApiKeyUnlist { get; set; }
        }

        public class OrganizationConfiguration
        {
            public string Name { get; set; }
            public string ApiKey { get; set; }
        }

        public class BrandingConfiguration
        {
            public string Message { get; set; }
            public string Url { get; set; }
            public string AboutUrl { get; set; }
            public string PrivacyPolicyUrl { get; set; }
            public string TermsOfUseUrl { get; set; }
            public string TrademarksUrl { get; set; }
        }
    }
}
