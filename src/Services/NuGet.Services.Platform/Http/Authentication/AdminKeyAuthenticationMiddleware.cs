using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Microsoft.Owin.Security.Infrastructure;

namespace NuGet.Services.Http.Authentication
{
    public class AdminKeyAuthenticationMiddleware : AuthenticationMiddleware<AdminKeyAuthenticationOptions>
    {
        public AdminKeyAuthenticationMiddleware(OwinMiddleware next, AdminKeyAuthenticationOptions options) : base(next, options) { }
        
        protected override AuthenticationHandler<AdminKeyAuthenticationOptions> CreateHandler()
        {
            return new AdminKeyAuthenticationHandler();
        }
    }
}

namespace Owin
{
    public static class AdminKeyAuthenticationMiddlewareExtensions
    {
        public static void UseAdminKeyAuthentication(this IAppBuilder self, NuGet.Services.Http.Authentication.AdminKeyAuthenticationOptions options)
        {
            self.Use(
                typeof(NuGet.Services.Http.Authentication.AdminKeyAuthenticationMiddleware),
                options);
        }
    }
}
