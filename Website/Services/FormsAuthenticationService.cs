using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Security;

namespace NuGetGallery
{
    public class FormsAuthenticationService : IFormsAuthenticationService
    {
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

            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(
                     version: 1,
                     name: userName,
                     issueDate: DateTime.UtcNow,
                     expiration: DateTime.UtcNow.AddMinutes(30),
                     isPersistent: createPersistentCookie,
                     userData: formattedRoles
            );

            string encryptedTicket = FormsAuthentication.Encrypt(ticket);
            var formsCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
            context.Response.Cookies.Add(formsCookie);
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }
    }
}