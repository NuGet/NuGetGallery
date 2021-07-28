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
            }

            Roles = "Admins";

            base.OnAuthorization(filterContext);
        }
    }
}