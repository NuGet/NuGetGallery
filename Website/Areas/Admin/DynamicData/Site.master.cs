using System;
using System.Net;
using System.Web.UI;
using NuGetGallery;

namespace NuGetGallery.Areas.Admin.DynamicData
{
    public partial class Site : MasterPage
    {
        protected override void OnInit(EventArgs e)
        {
            if (!Page.User.Identity.IsAuthenticated)
            {
                Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                Response.End();
            }

            if (!Request.IsLocal && !Page.User.IsAdministrator())
            {
                Response.StatusCode = (int)HttpStatusCode.Forbidden;
                Response.End();
            }

            base.OnInit(e);
        }
    }
}