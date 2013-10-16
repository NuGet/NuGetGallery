using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Logging;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Infrastructure;

namespace NuGetGallery.Authentication
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly ILogger _logger;

        public ApiKeyAuthenticationHandler(ILogger logger)
        {
            _logger = logger;
        }

        protected override async Task<AuthenticationTicket> AuthenticateCoreAsync()
        {
            var form = await Request.ReadFormAsync();
            var apiKey = form.Get(Options.ApiKeyFormName);
            if (!String.IsNullOrEmpty(apiKey))
            {
                var id = new ClaimsIdentity(
                    claims: new[] { 
                        new Claim(Options.ApiKeyClaim, apiKey)
                    },
                    authenticationType: Options.AuthenticationType);
                return new AuthenticationTicket(id, new AuthenticationProperties());
            }
            return null;
        }
    }
}
