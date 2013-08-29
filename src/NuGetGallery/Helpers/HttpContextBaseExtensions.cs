using System;
using System.Text;
using System.Web;
using System.Web.Security;
using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public static class HttpContextBaseExtensions
    {
        public static void SetConfirmationContext(this HttpContextBase httpContext, string returnUrl, string userAction)
        {
            var confirmationContext = new
            {
                Act = userAction,
                ReturnUrl = returnUrl,
            };
            string json = new JObject(confirmationContext).ToString();
            string protectedJson = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(json), "ConfirmationContext"));
            httpContext.Response.Cookies.Add(new HttpCookie("ConfirmationContext", protectedJson));
        }

        public static string GetConfirmationAction(this HttpContextBase httpContext)
        {
            var cookie = httpContext.Request.Cookies.Get("ConfirmationContext");
            var protectedJson = cookie.Value;
            var json = MachineKey.Unprotect(Convert.FromBase64String(protectedJson), "ConfirmationContext");
            dynamic confirmationContext = JObject.Parse(Encoding.UTF8.GetString(json));
            return (string)confirmationContext.ReturnUrl;
        }
    }
}