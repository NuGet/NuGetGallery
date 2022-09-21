// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class NuGetContext
    {
        private readonly Lazy<User> _currentUser;
        private readonly Lazy<User> _uploaderSpecialUserFallback;

        public NuGetContext(AppController ctrl)
        {
            Config = DependencyResolver.Current.GetService<IGalleryConfigurationService>();

            _currentUser = new Lazy<User>(() => ctrl.OwinContext.GetCurrentUser());
            _uploaderSpecialUserFallback = new Lazy<User>(() =>
            {
                var featureFlagService = DependencyResolver.Current.GetService<IFeatureFlagService>();
                User fallbackUser = null;
                if (featureFlagService.AreAnonymousUploadsEnabled())
                {
                    var userService = DependencyResolver.Current.GetService<IUserService>();
                    fallbackUser = userService.FindSpecialUserByRoleName(PackagesController.AnonymousUploadRoleName);
                }

                return fallbackUser;
            });
        }

        public IGalleryConfigurationService Config { get; internal set; }

        public User CurrentUser => _currentUser.Value;

        public User UploaderUser => _currentUser.Value ?? _uploaderSpecialUserFallback.Value;
    }
}