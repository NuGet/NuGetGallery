// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ValidateRecaptchaResponseForUploadsAttribute : ValidateRecaptchaResponseAttribute
    {
        private readonly IFeatureFlagService _featureFlagService;

        public ValidateRecaptchaResponseForUploadsAttribute(IFeatureFlagService featureFlagService) => _featureFlagService = featureFlagService;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (_featureFlagService.IsRecaptchaEnabledForUploads())
            {
                base.OnActionExecuting(filterContext);
            }
        }
    }
}