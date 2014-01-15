using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Web.WebPages;

namespace NuGetGallery
{
    public class SafeRedirectResult : ActionResult
    {
        public string Url { get; private set; }
        public string SafeUrl { get; private set; }

        public SafeRedirectResult(string url, string safeUrl)
        {
            Url = url;
            SafeUrl = safeUrl;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            if (String.IsNullOrWhiteSpace(Url) ||
                !context.RequestContext.HttpContext.Request.IsUrlLocalToHost(Url) ||
                Url.Length <= 1 ||
                IsValidLocalUrl(Url))
            {
                // Redirect to the safe url
                new RedirectResult(SafeUrl).ExecuteResult(context);
            }
            else
            {
                new RedirectResult(Url).ExecuteResult(context);
            }
        }

        private static bool IsValidLocalUrl(string Url)
        {
            if (!(Url.StartsWith("/", StringComparison.Ordinal) ||
                Url.StartsWith("//", StringComparison.Ordinal) ||
                Url.StartsWith("/\\", StringComparison.Ordinal)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}


