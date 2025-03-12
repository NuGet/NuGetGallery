// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace NuGetGallery.Helpers
{
    public static class CSPHelper
    {
        public static IHtmlString GetCSPNonce(this HtmlHelper helper)
        {
            var owinContext = helper.ViewContext.HttpContext.GetOwinContext();
            var cspNonce = string.Empty;
            var owinContextCSPNonce = owinContext.Get<string>("cspNonce");

            if (!string.IsNullOrEmpty(owinContextCSPNonce))
            {
                cspNonce = owinContextCSPNonce;
            }
            return new HtmlString(cspNonce);
        }
    }
}
