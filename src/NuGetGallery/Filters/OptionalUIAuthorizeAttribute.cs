// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public class OptionalUIAuthorizeAttribute : UIAuthorizeAttribute
    {
        public OptionalUIAuthorizeAttribute(bool allowDiscontinuedLogins = false): base(allowDiscontinuedLogins)
        {
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!DependencyResolver.Current.GetService<IFeatureFlagService>().AreAnonymousUploadsEnabled())
            {
                base.OnAuthorization(filterContext);
            }
        }
    }
}