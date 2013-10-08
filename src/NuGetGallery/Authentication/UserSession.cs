using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace NuGetGallery.Authentication
{
    public class UserSession : IPrincipal
    {
        public ClaimsPrincipal Principal { get; private set; }

        public string Username { get { return Principal.Identity.Name; } }
        public string AuthenticationType { get { return Principal.Identity.AuthenticationType; } }
    
        public IIdentity Identity
        {
	        get { return Principal.Identity; }
        }

        public UserSession(ClaimsPrincipal principal)
        {
            Principal = principal;
        }

        public bool IsInRole(string role)
        {
            return Principal.IsInRole(role);
        }
    }
}
