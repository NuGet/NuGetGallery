using System;
using System.Text;
using System.Web;
using System.Web.Security;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public static class HttpContextBaseExtensions
    {
        public static void SetConfirmationContext(this HttpContextBase httpContext, string userAction, string returnUrl)
        {
            var confirmationContext = new ConfirmationContext
            {
                Act = userAction,
                ReturnUrl = returnUrl,
            };
            string json = JsonConvert.SerializeObject(confirmationContext);
            string protectedJson = Convert.ToBase64String(MachineKey.Protect(Encoding.UTF8.GetBytes(json), "ConfirmationContext"));
            httpContext.Response.Cookies.Add(new HttpCookie("ConfirmationContext", protectedJson));
        }

        public static string GetConfirmationAction(this HttpContextBase httpContext)
        {
            var cookie = httpContext.Request.Cookies.Get("ConfirmationContext");
            var protectedJson = cookie.Value;
            string json = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(protectedJson), "ConfirmationContext"));
            var confirmationContext = JsonConvert.DeserializeObject<ConfirmationContext>(json);
            return confirmationContext.Act;
        }

        public static string GetConfirmationReturnUrl(this HttpContextBase httpContext)
        {
            var cookie = httpContext.Request.Cookies.Get("ConfirmationContext");
            var protectedJson = cookie.Value;
            if (String.IsNullOrEmpty(protectedJson))
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(MachineKey.Unprotect(Convert.FromBase64String(protectedJson), "ConfirmationContext"));
            var confirmationContext = JsonConvert.DeserializeObject<ConfirmationContext>(json);
            return confirmationContext.ReturnUrl;
        }
    }

    public class ConfirmationContext
    {
        public string Act { get; set; }
        public string ReturnUrl { get; set; }
    }
}