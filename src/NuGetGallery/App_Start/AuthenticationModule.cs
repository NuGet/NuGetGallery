using System;
using System.Security.Principal;
using System.Threading;
using System.Web;
using System.Web.Security;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using NuGetGallery;

[assembly: WebActivator.PreApplicationStartMethod(typeof(AuthenticationModule), "Start")]

namespace NuGetGallery
{
    public class AuthenticationModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.AuthenticateRequest += OnAuthenticateRequest;
        }

        public void Dispose()
        {
        }

        public static void Start()
        {
            //DynamicModuleUtility.RegisterModule(typeof(AuthenticationModule));
        }

        private void OnAuthenticateRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            var request = HttpContext.Current.Request;
            if (request.IsAuthenticated)
            {
                HttpCookie authCookie = request.Cookies[FormsAuthentication.FormsCookieName];
                if (authCookie != null)
                {
                    FormsAuthenticationTicket authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                    var roles = authTicket.UserData.Split('|');
                    var user = new GenericPrincipal(context.User.Identity, roles);
                    context.User = Thread.CurrentPrincipal = user;
                }
            }
        }
    }
}