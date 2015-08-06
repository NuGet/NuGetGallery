// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class NuGetContext
    {
        private readonly Lazy<User> _currentUser;

        public NuGetContext(AppController ctrl)
        {
            Config = DependencyResolver.Current.GetService<ConfigurationService>();

            _currentUser = new Lazy<User>(() => ctrl.OwinContext.GetCurrentUser());
        }

        public ConfigurationService Config { get; internal set; }
        public User CurrentUser { get { return _currentUser.Value; } }
    }
}