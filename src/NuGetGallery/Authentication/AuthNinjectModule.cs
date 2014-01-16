using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Ninject.Modules;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery.Authentication
{
    public class AuthNinjectModule : NinjectModule
    {
        public override void Load()
        {
            foreach (var instance in Authenticator.GetAllAvailable())
            {
                Bind(typeof(Authenticator))
                    .ToConstant(instance)
                    .InSingletonScope();
            }
        }
    }
}