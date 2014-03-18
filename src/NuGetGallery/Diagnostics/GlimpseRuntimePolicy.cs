using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Policy;
using NuGetGallery.Configuration;

namespace NuGetGallery.Diagnostics
{
    public class GlimpseRuntimePolicy : IRuntimePolicy
    {
        public IAppConfiguration Configuration { get; protected set; }

        protected GlimpseRuntimePolicy()
        {
        }

        public GlimpseRuntimePolicy(IAppConfiguration configuration)
        {
            Configuration = configuration;
        }

        public RuntimeEvent ExecuteOn
        {
            get { return RuntimeEvent.BeginSessionAccess; }
        }

        public RuntimePolicy Execute(IRuntimePolicyContext policyContext)
        {
            return Execute(policyContext.GetRequestContext<HttpContextBase>());
        }

        public RuntimePolicy Execute(HttpContextBase context)
        {
            // Disable Glimpse for any static content
            var path = context.Request.Path.TrimStart('/');
            if(path.StartsWith("public", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("content", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("scripts", StringComparison.OrdinalIgnoreCase))
            {
                return RuntimePolicy.Off;
            }

            // Policy is: Localhost collects data, admins always see Glimpse (even when remote) but only over SSL if SSL is required, everyone uses the setting in web config.
            if (context.Request.IsAuthenticated &&
                 (!Configuration.RequireSSL || context.Request.IsSecureConnection) &&
                 context.User.IsAdministrator())
            {
                return RuntimePolicy.On;
            }
            else if (context.Request.IsLocal)
            {
                return RuntimePolicy.PersistResults;
            }
            return RuntimePolicy.Off;
        }
    }
}