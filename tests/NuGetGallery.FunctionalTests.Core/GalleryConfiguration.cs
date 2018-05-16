using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
        public IEnumerable<string> TrustedHttpsCertificates { get; private set; }
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

        static GalleryConfiguration()
        {
            var configurationString = File.ReadAllText(EnvironmentSettings.ConfigurationFilePath);
            Instance = JsonConvert.DeserializeObject<GalleryConfiguration>(configurationString);
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
