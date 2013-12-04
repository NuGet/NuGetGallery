using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services;

namespace System.Security.Principal
{
    public static class PrincipalExtensions
    {
        public static bool SafeIsAdmin(this IPrincipal self)
        {
            return self != null && self.IsInRole(Constants.AdminRoleName);
        }
    }
}
