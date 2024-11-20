// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Mvc;

namespace NuGetGallery.Filters
{
    public class StrictCSPFilter : FilterAttribute, IActionFilter
    {
        public void OnActionExecuted(ActionExecutedContext filterContext)
        {
            return;
        }

        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var regexStr = "https://res-1.cdn.office.net/files/fabric-cdn-prod_20221201.001/assets/";


            var rng = new RNGCryptoServiceProvider();
            var nonceBytes = new byte[32];
            rng.GetBytes(nonceBytes);
            var nonce = Convert.ToBase64String(nonceBytes);
            filterContext.HttpContext.Response.AppendHeader("Content-Security-Policy-Read-only", string.Format("default-src 'self' 'nonce-{0}'; script-src 'self' https://localhost/ https://wcpstatic.microsoft.com/mscc/lib/v2/ 'nonce-{0}' 'unsafe-inline' 'unsafe-eval'; font-src 'self' {1} 'nonce-{0}'; base-uri 'none'; form-action 'self' 'nonce-{0}'; style-src 'self' 'nonce-{0}' 'unsafe-inline'", nonce,regexStr)
                );
         
            Debug.WriteLine("Request URL: " + filterContext.HttpContext.Request.RawUrl.ToString());
            Debug.WriteLine(filterContext.HttpContext.Request);
        }
    }
}
