// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using Autofac.Features.Indexed;
using NuGet.Services.Sql;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Areas.Admin.DynamicData;

namespace NuGetGallery.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public const string Name = "Admin";

        public override string AreaName => Name;

        public override void RegisterArea(AreaRegistrationContext context)
        {
            var galleryDbConnectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();

            context.Routes.Ignore("Admin/Errors.axd/{*pathInfo}"); // ELMAH owns this root
            DynamicDataManager.Register(context.Routes, "Admin/Database", galleryDbConnectionFactory);

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = ActionName.AdminHomeIndex, id = UrlParameter.Optional }
            );
        }
    }
}
