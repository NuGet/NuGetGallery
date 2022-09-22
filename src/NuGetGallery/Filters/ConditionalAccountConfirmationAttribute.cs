// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    public sealed class ConditionalAccountConfirmationAttribute : RequiresAccountConfirmationAttribute
    {
        private FilterConditions _conditions;

        public ConditionalAccountConfirmationAttribute(string inOrderTo, FilterConditions conditions): base(inOrderTo)
        {
            _conditions = conditions;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // We can't lazy or cache this result as the feature flag(s) may change during a session
            if (FilterHelper.EvaluateFilterConditions(_conditions))
            {
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}