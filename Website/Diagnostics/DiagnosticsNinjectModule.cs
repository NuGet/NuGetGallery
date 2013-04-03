using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Glimpse.Core.Extensibility;
using Ninject.Modules;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsNinjectModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IRuntimePolicy>()
                .To<NuGetGlimpseRuntimePolicy>();
        }
    }
}