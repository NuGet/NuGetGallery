// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public static class AdminHelper
    {
        private static Lazy<bool> AdminPanelEnabled = new Lazy<bool>(() =>  
            DependencyResolver.Current.GetService<IGalleryConfigurationService>()
                .Current.AdminPanelEnabled);

        public static bool IsAdminPanelEnabled => AdminPanelEnabled.Value;
    }
}