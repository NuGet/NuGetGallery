// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web.Mvc;
using System.Web.Routing;

namespace NuGetGallery
{
    public class NuGetControllerFactory : DefaultControllerFactory
    {
        protected override IController GetControllerInstance(RequestContext requestContext, Type controllerType)
        {
            var controller = base.GetControllerInstance(requestContext, controllerType);
            var controllerBase = controller as Controller;
            controllerBase.TempDataProvider = new CookieTempDataProvider(requestContext.HttpContext);
            return controller;
        }
    }
}