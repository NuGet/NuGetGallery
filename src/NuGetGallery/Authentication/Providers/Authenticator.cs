// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using NuGetGallery.Services.Authentication;
using Owin;

namespace NuGetGallery.Authentication.Providers
{
    public abstract class Authenticator
    {
        public const string AuthPrefix = "Auth.";
        private static readonly Regex NameShortener = new Regex(@"^(?<shortname>[A-Za-z0-9_]*)Authenticator$");

        public AuthenticatorConfiguration BaseConfig { get; private set; }

        public virtual string Name
        {
            get { return GetName(GetType()); }
        }

        protected Authenticator()
        {
            BaseConfig = CreateConfigObject();
        }

        public async Task Startup(IGalleryConfigurationService config, IAppBuilder app)
        {
            await Configure(config);

            if (BaseConfig.Enabled)
            {
                AttachToOwinApp(config, app);
            }
        }

        protected virtual void AttachToOwinApp(IGalleryConfigurationService config, IAppBuilder app) { }

        // Configuration Logic
        protected virtual async Task Configure(IGalleryConfigurationService config)
        {
            BaseConfig = await config.ResolveConfigObject(BaseConfig, AuthPrefix + Name + ".");
        }

        public static string GetName(Type authenticator)
        {
            var name = authenticator.Name;
            var match = NameShortener.Match(name);
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

        public virtual ActionResult Challenge(string redirectUrl, AuthenticationPolicy policy)
        {
            return new HttpUnauthorizedResult();
        }

        /// <summary>
        /// Override this method to provide confirmation on identity author.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity returned by the identity</param>
        /// <returns>Returns true if this provider is the author for the identity, false otherwise</returns>
        public virtual bool IsProviderForIdentity(ClaimsIdentity claimsIdentity)
        {
            // If the issuer of the claims identity is same as that of the authentication type then this is the author.
            var firstClaim = claimsIdentity?.Claims?.FirstOrDefault();
            if (firstClaim == null)
            {
                return false;
            }

            return string.Equals(firstClaim.Issuer, BaseConfig.AuthenticationType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The providers will override this method to extract the user information
        /// from the returned claims by the identity authentication.
        /// </summary>
        /// <param name="claimsIdentity">The claims identity returned by the identity</param>
        /// <returns><see cref="IdentityInformation"/></returns>
        public virtual IdentityInformation GetIdentityInformation(ClaimsIdentity claimsIdentity)
        {
            return null;
        }

        /// <summary>
        /// Check if the provider supports multi-factor authentication
        /// </summary>
        /// <returns>Returns true if the provider supports multi-factor authentication</returns>
        public virtual bool SupportsMultiFactorAuthentication()
        {
            return false;
        }
    }
}