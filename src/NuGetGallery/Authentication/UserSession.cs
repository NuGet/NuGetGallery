using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;

namespace NuGetGallery.Authentication
{
    public class UserSession : IPrincipal, IIdentity
    {
        public virtual ClaimsPrincipal Principal { get; private set; }

        public virtual string AuthenticationType
        {
            get { return Principal.Identity.AuthenticationType; }
        }

        public virtual bool IsAuthenticated
        {
            get { return Principal.Identity.IsAuthenticated; }
        }

        public virtual string Name
        {
            get { return Principal.Identity.Name;  }
        }

        public virtual IIdentity Identity
        {
            get { return Principal.Identity; }
        }

        public UserSession(ClaimsPrincipal principal)
        {
            Principal = principal;
        }

        public virtual bool IsInRole(string role)
        {
            return Principal.IsInRole(role);
        }
    }
}