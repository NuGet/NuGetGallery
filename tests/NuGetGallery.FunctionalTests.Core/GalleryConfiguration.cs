// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NuGetGallery.FunctionalTests
{
    public class GalleryConfiguration
    {
        public static GalleryConfiguration Instance;

        public string GalleryBaseUrl => "staging".Equals(Slot, StringComparison.OrdinalIgnoreCase) ? StagingBaseUrl : ProductionBaseUrl;

        [JsonProperty]
        private string Slot { get; set; }
        [JsonProperty]
        private string ProductionBaseUrl { get; set; }
        [JsonProperty]
        private string StagingBaseUrl { get; set; }
        [JsonProperty]
        public string SearchServiceBaseUrl { get; private set; }
        [JsonProperty]
        public string EmailServerHost { get; private set; }
        [JsonProperty]
        public bool DefaultSecurityPoliciesEnforced { get; private set; }
        [JsonProperty]
        public bool TestPackageLock { get; private set; }
        [JsonProperty]
        public AccountConfiguration Account { get; private set; }
        [JsonProperty]
        public OrganizationConfiguration AdminOrganization { get; private set; }
        [JsonProperty]
        public OrganizationConfiguration CollaboratorOrganization { get; private set; }
        [JsonProperty]
        public BrandingConfiguration Branding { get; private set; }
        [JsonProperty]
        public bool TyposquattingCheckAndBlockUsers { get; private set; }

        static GalleryConfiguration()
        {
            try
            {
                var configurationFilePath = EnvironmentSettings.ConfigurationFilePath;
                var configurationString = File.ReadAllText(configurationFilePath);
                Instance = JsonConvert.DeserializeObject<GalleryConfiguration>(configurationString);
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

        public class AccountConfiguration : OrganizationConfiguration
        {
            [JsonProperty]
            public string Email { get; private set; }
            [JsonProperty]
            public string Password { get; private set; }
            [JsonProperty]
            public string ApiKeyPush { get; private set; }
            [JsonProperty]
            public string ApiKeyPushVersion { get; private set; }
            [JsonProperty]
            public string ApiKeyUnlist { get; private set; }
        }

        public class OrganizationConfiguration
        {
            [JsonProperty]
            public string Name { get; private set; }
            [JsonProperty]
            public string ApiKey { get; private set; }
        }

        public class BrandingConfiguration
        {
            [JsonProperty]
            public string Message { get; private set; }
            [JsonProperty]
            public string Url { get; private set; }
            [JsonProperty]
            public string AboutUrl { get; private set; }
            [JsonProperty]
            public string PrivacyPolicyUrl { get; private set; }
            [JsonProperty]
            public string TermsOfUseUrl { get; private set; }
            [JsonProperty]
            public string TrademarksUrl { get; private set; }
        }
    }
}
