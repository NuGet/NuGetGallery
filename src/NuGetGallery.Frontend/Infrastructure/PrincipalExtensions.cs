using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web;

namespace NuGetGallery
{
    public static class PrincipalExtensions
    {
        public static bool IsAdministrator(this IPrincipal self)
        {
            return self.IsInRole(Constants.AdminRoleName);
        }
    }
}