using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.AspNet.Extensibility;
using Glimpse.Core.Extensibility;

namespace NuGetGallery.Diagnostics
{
    public class AuthenticationGlimpseTab : AspNetTab
    {
        public override object GetData(ITabContext context)
        {
            return context.GetRequestContext<HttpContextBase>().User;
        }

        public override string Name
        {
            get { return "Auth"; }
        }
    }
}