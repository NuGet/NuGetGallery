using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Framework;
using Glimpse.Core.Policy;
using Microsoft.WindowsAzure.ServiceRuntime;
using Ninject.Modules;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsNinjectModule : NinjectModule
    {
        public override void Load()
        {
            // Glimpse Policies
            Bind<IRuntimePolicy>()
                .To<GlimpseRuntimePolicy>()
                .InSingletonScope();
            Bind<IRuntimePolicy>()
                .To<GlimpseResourcePolicy>()
                .InSingletonScope();
            Bind<IRuntimePolicy>()
                .To<UriPolicy>()
                .InSingletonScope()
                // Just to prevent known-static files from being Glimpsed.
                .WithConstructorArgument("uriBlackList", new List<Regex> {
                    new Regex(@"^.*/Content/.*$"),
                    new Regex(@"^.*/Scripts/.*$"),
                    new Regex(@"^.*(Web|Script)Resource\.axd.*$")
                });

            Bind<IDiagnosticsService>()
                .To<DiagnosticsService>()
                .InSingletonScope();


            // Persistence configuration. In memory only (for now).
            Bind<IPersistenceStore>()
                .To<ConcurrentInMemoryPersistenceStore>()
                .InSingletonScope();
        }
    }
}