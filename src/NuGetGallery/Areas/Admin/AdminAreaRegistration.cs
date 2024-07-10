// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using NuGet.Services.Sql;
using NuGetGallery.Areas.Admin.DynamicData;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin
{
    public class AdminAreaRegistration : AreaRegistration
    {
        public const string Name = "Admin";

        public override string AreaName => Name;

        public override void RegisterArea(AreaRegistrationContext context)
        {
            var galleryConfigurationService = DependencyResolver.Current.GetService<IGalleryConfigurationService>();

            if (!galleryConfigurationService.Current.AdminPanelEnabled)
            {
                return;
            }

            if (galleryConfigurationService.Current.AdminPanelDatabaseAccessEnabled)
            {
                var galleryDbConnectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
                DynamicDataManager.Register(context.Routes, "Admin/Database", galleryDbConnectionFactory);
            }

            context.MapRoute(
                "Admin_default",
                "Admin/{controller}/{action}/{id}",
                new { controller="Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
