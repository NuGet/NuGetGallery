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

        public virtual ActionResult Challenge(string redirectUrl)
        {
            return new HttpUnauthorizedResult();
        }

        public virtual bool IsAuthorForIdentity(ClaimsIdentity claimsIdentity)
        {
            // If the issuer of the claims identity is same as that of the authentication type then this is the author.
            Claim firstClaim = claimsIdentity.Claims.FirstOrDefault();
            if (firstClaim == null)
            {
                return false;
            }

            return string.Equals(firstClaim.Issuer, BaseConfig.AuthenticationType, StringComparison.OrdinalIgnoreCase);
        }

        public virtual AuthInformation GetAuthInformation(ClaimsIdentity claimsIdentity)
        {
            if (!IsAuthorForIdentity(claimsIdentity))
            {
                throw new ArgumentException($"The identity not authored by {BaseConfig.AuthenticationType}");
            }

            var identifierClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if (identifierClaim == null)
            {
                throw new ArgumentException("External Authentication is missing required claim: " + ClaimTypes.NameIdentifier);
            }

            var nameClaim = claimsIdentity.FindFirst(ClaimTypes.Name);
            if (nameClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {ClaimTypes.Name}");
            }

            var emailClaim = claimsIdentity.FindFirst(ClaimTypes.Email);
            if (emailClaim == null)
            {
                throw new ArgumentException($"External Authentication is missing required claim: {ClaimTypes.Email}");
            }

            return new AuthInformation(identifierClaim.Value, nameClaim.Value, emailClaim.Value, BaseConfig.AuthenticationType, tenantId: null);
        }
    }
}