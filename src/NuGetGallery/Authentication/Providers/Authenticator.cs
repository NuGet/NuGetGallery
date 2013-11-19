using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers
{
    public abstract class Authenticator
    {
        private static readonly Regex nameShortener = new Regex(@"^(?<shortname>[A-Za-z0-9_]*)Authenticator$");
        private static readonly string AuthPrefix = "Auth.";
    
        public AuthenticatorConfiguration BaseConfig { get; private set; }

        public virtual string Name
        {
            get { return GetName(GetType()); }
        }

        protected Authenticator()
        {
            BaseConfig = CreateConfigObject();
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
            BaseConfig = config.ResolveConfigObject(BaseConfig, AuthPrefix + Name + ".");
        }

        public static string GetName(Type authenticator)
        {
            var name = authenticator.Name;
            var match = nameShortener.Match(name);
            if (match.Success)
            {
                name = match.Groups["shortname"].Value;
            }
            return name; 
        }

        internal static IEnumerable<Authenticator> GetAllAvailable()
        {
            // Find all available auth providers
            return GetAllAvailable(typeof(Authenticator)
                .Assembly
                .GetExportedTypes());
        }

        internal static IEnumerable<Authenticator> GetAllAvailable(IEnumerable<Type> typesToSearch)
        {
            // Find all available auth providers
            var configTypes = 
                typesToSearch
                .Where(t => !t.IsAbstract && typeof(Authenticator).IsAssignableFrom(t))
                .ToList();
            var providers = configTypes
                .Select(t => (Authenticator)Activator.CreateInstance(t))
                .ToList();
            return providers;
        }

        protected internal virtual AuthenticatorConfiguration CreateConfigObject()
        {
            return new AuthenticatorConfiguration();
        }

        public virtual AuthenticatorUI GetUI()
        {
            return null;
        }

        public virtual ActionResult Challenge(string redirectUrl)
        {
            return new HttpUnauthorizedResult();
        }
    }

    public abstract class Authenticator<TConfig> : Authenticator 
        where TConfig : AuthenticatorConfiguration, new()
    {
        public TConfig Config { get; private set; }

        protected internal override AuthenticatorConfiguration CreateConfigObject()
        {
            Config = new TConfig();
            return Config;
        }
    }
}