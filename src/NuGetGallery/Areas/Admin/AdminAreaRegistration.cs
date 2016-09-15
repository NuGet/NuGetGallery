// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using NuGetGallery.Areas.Admin.DynamicData;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "Admin";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            var configService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();

            context.Routes.Ignore("Admin/Errors.axd/{*pathInfo}"); // ELMAH owns this root
            DynamicDataManager.Register(context.Routes, "Admin/Database", configService);

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
