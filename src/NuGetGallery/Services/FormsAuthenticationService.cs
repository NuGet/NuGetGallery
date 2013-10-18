using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Security;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class FormsAuthenticationService : IFormsAuthenticationService
    {
        private readonly IAppConfiguration _configuration;

        public FormsAuthenticationService(IAppConfiguration configuration)
        {
            _configuration = configuration;
        }

        private const string ForceSSLCookieName = "ForceSSL";

        public void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles)
        {
            string formattedRoles = String.Empty;
            if (roles.AnySafe())
            {
                formattedRoles = String.Join("|", roles);
            }

            HttpContext context = HttpContext.Current;

            var ticket = new FormsAuthenticationTicket(
                version: 1,
                name: userName,
                issueDate: DateTime.UtcNow,
                expiration: DateTime.UtcNow.AddYears(200),
                isPersistent: createPersistentCookie,
                userData: formattedRoles
                );

            string encryptedTicket = FormsAuthentication.Encrypt(ticket);
            var formsCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = _configuration.RequireSSL
            };
            context.Response.Cookies.Add(formsCookie);

            if (_configuration.RequireSSL)
            {
                // Drop a second cookie indicating that the user is logged in via SSL (no secret data, just tells us to redirect them to SSL)
                context.Response.Cookies.Add(new HttpCookie(ForceSSLCookieName, "true"));
            }
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();

            // Delete the "LoggedIn" cookie
            HttpContext context = HttpContext.Current;
            var cookie = context.Request.Cookies[ForceSSLCookieName];
            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddDays(-1d);
                context.Response.Cookies.Add(cookie);
            }
        }


        public bool ShouldForceSSL(HttpContextBase context)
        {
            var cookie = context.Request.Cookies[ForceSSLCookieName];
            
            bool value;
            if (cookie != null && Boolean.TryParse(cookie.Value, out value))
            {
                return value;
            }
            
            return false;
        }
    }
}