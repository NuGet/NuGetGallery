// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class ConfigController : AdminControllerBase
    {
        private readonly IGalleryConfigurationService _config;
        private readonly AuthenticationService _auth;

        public ConfigController(IGalleryConfigurationService config, AuthenticationService auth)
        {
            _config = config;
            _auth = auth;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var settings = (from p in typeof(IAppConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        where p.CanRead
                        select p)
                       .ToDictionary(p => p.Name, p =>
                       {
                           var propertyType = p.PropertyType;
                           var propertyValue = p.GetValue(_config.Current);

                           if (propertyValue != null && p.Name.ToLowerInvariant().Contains("connectionstring"))
                           {
                               propertyValue = new string('*', 10);
                           }

                           return Tuple.Create(propertyType, propertyValue);
                       });

            var features = (from p in typeof(FeatureConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            where p.CanRead
                            select new FeatureConfigViewModel(p, _config.Features))
                            .ToList();


            var configModel = new ConfigViewModel(settings, features, _auth.Authenticators.Values);

            return View(configModel);
        }
    }
}