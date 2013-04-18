using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Mindscape.Raygun4Net;

namespace NuGetGallery.App_Start
{
    public class NuGetRaygunModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.Error += context_Error;
        }

        void context_Error(object sender, EventArgs e)
        {
            var ex = HttpContext.Current.Server.GetLastError();
            if (ex is HttpUnhandledException || ex is AggregateException)
            {
                ex = ex.GetBaseException();
            }
            new RaygunClient("Nope, Chuck Testa!").SendInBackground(ex);
        }
    }
}
