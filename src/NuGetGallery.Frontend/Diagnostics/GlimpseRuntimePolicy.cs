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
            // Policy is: Localhost sees everything, admins always see Glimpse (even when remote) but only over SSL if SSL is required, everyone uses the setting in web config.
            if (context.Request.IsLocal ||
                (context.Request.IsAuthenticated &&
                 (!Configuration.RequireSSL || context.Request.IsSecureConnection) &&
                 context.User.IsAdministrator()))
            {
                return RuntimePolicy.On;
            }
            return RuntimePolicy.Off;
        }
    }
}