using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class NuGetGlimpseRuntimePolicy : IRuntimePolicy
    {
        public RuntimeEvent ExecuteOn
        {
            get { return RuntimeEvent.BeginSessionAccess | RuntimeEvent.ExecuteResource; }
        }

        public RuntimePolicy Execute(IRuntimePolicyContext policyContext)
        {
            return Execute(policyContext.GetRequestContext<HttpContextBase>());
        }

        public RuntimePolicy Execute(HttpContextBase context)
        {
            // Policy is: Admins see Glimpse, everyone records Glimpse data
            if (context.Request.IsAuthenticated && 
                context.Request.IsSecureConnection &&
                context.User.IsAdministrator())
            {
                return RuntimePolicy.On;
            }
            return RuntimePolicy.PersistResults;
        }
    }
}