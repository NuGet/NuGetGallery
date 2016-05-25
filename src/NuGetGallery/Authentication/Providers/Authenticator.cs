﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using Owin;

namespace NuGetGallery.Authentication.Providers
{
    public abstract class Authenticator
    {
        private static readonly Regex NameShortener = new Regex(@"^(?<shortname>[A-Za-z0-9_]*)Authenticator$");
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

        public virtual bool TryMapIssuerToAuthenticationType(string issuer, out string authenticationType)
        {
            if (string.Equals(issuer, BaseConfig.AuthenticationType, StringComparison.OrdinalIgnoreCase))
            {
                authenticationType = BaseConfig.AuthenticationType;
                return true;
            }

            authenticationType = null;
            return false;
        }
    }
}