// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    public class AdminActionAttribute : UIAuthorizeAttribute
    {
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (!AdminHelper.IsAdminPanelEnabled)
            {
                filterContext.Result = new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                return;
            }

            Roles = "Admins";

            base.OnAuthorization(filterContext);
        }
    }
}