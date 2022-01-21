// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ValidateRecaptchaResponseForUploadsAttribute : ValidateRecaptchaResponseAttribute
    {
        private const string RecaptchaEnabled = "recaptcha-enabled";

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = filterContext.Controller as AppController;
            var recaptchaEnabled = controller.HttpContext.Request.Form[RecaptchaEnabled];
            if (recaptchaEnabled == "true")
            {
                base.OnActionExecuting(filterContext);
            }
        }
    }
}