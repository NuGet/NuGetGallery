using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class NuGetRuntimePolicy : IRuntimePolicy
    {
        public RuntimePolicy Execute(IRuntimePolicyContext policyContext)
        {
            var context = policyContext.GetRequestContext<HttpContextBase>();
            if (context.Request.IsLocal || (context.Request.IsSecureConnection && context.User.IsInRole(Constants.AdminRoleName)))
            {
                return RuntimePolicy.On;
            }
            return RuntimePolicy.PersistResults;
        }

        public RuntimeEvent ExecuteOn
        {
            get { return RuntimeEvent.BeginRequest; }
        }
    }
}
