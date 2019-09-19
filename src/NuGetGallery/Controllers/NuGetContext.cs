// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Cookies;

namespace NuGetGallery
{
    public class NuGetContext
    {
        private readonly Lazy<User> _currentUser;

        public NuGetContext(AppController ctrl)
        {
            Config = DependencyResolver.Current.GetService<IGalleryConfigurationService>();
            CookieComplianceService = DependencyResolver.Current.GetService<ICookieComplianceService>();
            Features = DependencyResolver.Current.GetService<IFeatureFlagService>();

            _currentUser = new Lazy<User>(() => ctrl.OwinContext.GetCurrentUser());
        }

        public IGalleryConfigurationService Config { get; internal set; }

        public User CurrentUser { get { return _currentUser.Value; } }

        public IFeatureFlagService Features { get; }

        private ICookieComplianceService CookieComplianceService { get; }

        public CookieConsentMessage GetCookieConsentMessage(HttpRequestBase request)
        {
            if (CookieComplianceService.NeedsConsentForNonEssentialCookies(request))
            {
                return CookieComplianceService.GetConsentMessage(request);
            }
            return null;
        }
    }
}