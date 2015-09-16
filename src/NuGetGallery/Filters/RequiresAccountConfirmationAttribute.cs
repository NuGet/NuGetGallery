﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Globalization;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class RequiresAccountConfirmationAttribute : ActionFilterAttribute
    {
        private readonly string _inOrderTo;

        public RequiresAccountConfirmationAttribute(string inOrderTo)
        {
            _inOrderTo = inOrderTo;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            if (!filterContext.HttpContext.Request.IsAuthenticated)
            {
                throw new InvalidOperationException("Requires account confirmation attribute is only valid on authenticated actions.");
            }
            
            var controller = ((AppController)filterContext.Controller);
            var user = controller.GetCurrentUser();
            
            if (!user.Confirmed)
            {
                controller.TempData["ConfirmationRequiredMessage"] = string.Format(
                    CultureInfo.CurrentCulture,
                    "Before you can {0} you must first confirm your email address.", _inOrderTo);
                controller.HttpContext.SetConfirmationReturnUrl(controller.Url.Current());
                filterContext.Result = new RedirectResult(controller.Url.ConfirmationRequired());
            }
        }
    }
}