// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.DynamicData;
using Ninject;
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
            var config = Container.Kernel.Get<IAppConfiguration>();

            context.Routes.Ignore("Admin/Errors.axd/{*pathInfo}"); // ELMAH owns this root
            context.Routes.Ignore("Admin/Glimpse/{*pathInfo}"); // Glimpse owns this root
            DynamicDataManager.Register(context.Routes, "Admin/Database", config);

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
