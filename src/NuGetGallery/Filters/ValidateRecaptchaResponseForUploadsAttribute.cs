// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ValidateRecaptchaResponseForUploadsAttribute : ValidateRecaptchaResponseAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var recaptchaEnabledData = filterContext.Controller?.TempData?[GalleryConstants.RecaptchaEnabled] ?? string.Empty;
            if (recaptchaEnabledData is string recaptchaEnabled && recaptchaEnabled == "true")
            {
                base.OnActionExecuting(filterContext);
            }
        }
    }
}