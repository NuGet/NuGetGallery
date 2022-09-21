// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Globalization;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class OptionalAccountConfirmationAttribute : RequiresAccountConfirmationAttribute
    {
        public OptionalAccountConfirmationAttribute(string inOrderTo): base(inOrderTo)
        {
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException(nameof(filterContext));
            }

            if (DependencyResolver.Current.GetService<IFeatureFlagService>().AreAnonymousUploadsEnabled())
            {
                return;
            }

            if (!filterContext.HttpContext.Request.IsAuthenticated)
            {
                throw new InvalidOperationException("Requires account confirmation attribute is only valid on authenticated actions.");
            }

            VerifyUser(filterContext);
        }
    }
}