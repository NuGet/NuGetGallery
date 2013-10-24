using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers
{
    public abstract class AuthenticationProvider
    {
        private static readonly Regex nameShortener = new Regex(@"^(?<shortname>[A-Za-z0-9_]*)AuthenticationProvider$");
        private static readonly string AuthPrefix = "Auth.";
    
        public AuthenticationProviderConfiguration BaseConfig { get; private set; }
        
        public virtual string Name
        {
            get
            {
                var name = GetType().Name;
                var match = nameShortener.Match(name);
                if (match.Success)
                {
                    name = match.Groups["shortname"].Value;
                }
                return name;
            }
        }

        public void Startup(ConfigurationService config, IAppBuilder app)
        {
            Configure(config);

            if (BaseConfig.Enabled)
            {
                AttachToOwinApp(config, app);
            }
        }

        protected virtual void AttachToOwinApp(ConfigurationService config, IAppBuilder app) { }

        // Configuration Logic
        public virtual void Configure(ConfigurationService config)
        {
            BaseConfig = config.ResolveConfigObject(CreateConfigObject(), AuthPrefix + Name + ".");
        }

        internal static IEnumerable<AuthenticationProvider> GetAllAvailable()
        {
            // Find all available auth providers
            var configTypes = typeof(ConfigurationService)
                .Assembly
                .GetExportedTypes()
                .Where(t => !t.IsAbstract && typeof(AuthenticationProvider).IsAssignableFrom(t))
                .ToList();
            var providers = configTypes
                .Select(t => (AuthenticationProvider)Activator.CreateInstance(t))
                .ToList();
            return providers;
        }

        protected virtual AuthenticationProviderConfiguration CreateConfigObject()
        {
            return new AuthenticationProviderConfiguration();
        }
    }

    public abstract class AuthenticationProvider<TConfig> : AuthenticationProvider 
        where TConfig : AuthenticationProviderConfiguration, new()
    {
        public TConfig Config { get; private set; }

        protected override AuthenticationProviderConfiguration CreateConfigObject()
        {
            Config = new TConfig();
            return Config;
        }
    }
}