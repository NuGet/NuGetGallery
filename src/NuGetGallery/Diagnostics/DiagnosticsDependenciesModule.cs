// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autofac;
using Glimpse.Core.Extensibility;
using Glimpse.Core.Framework;
using Glimpse.Core.Policy;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsDependenciesModule 
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Glimpse Policies
            builder.RegisterType<GlimpseRuntimePolicy>()
                .AsSelf()
                .As<IRuntimePolicy>()
                .SingleInstance();
            builder.RegisterType<GlimpseResourcePolicy>()
                .AsSelf()
                .As<IRuntimePolicy>()
                .SingleInstance();
            builder.RegisterInstance(new UriPolicy(new List<Regex> {
                        new Regex(@"^.*/Content/.*$"),
                        new Regex(@"^.*/Scripts/.*$"),
                        new Regex(@"^.*(Web|Script)Resource\.axd.*$")
                    }))
                .AsSelf()
                .As<IRuntimePolicy>()
                .SingleInstance();

            builder.RegisterType<DiagnosticsService>()
                .AsSelf()
                .As<IDiagnosticsService>()
                .SingleInstance();

            // Persistence configuration. In memory only (for now).
            builder.RegisterType<ConcurrentInMemoryPersistenceStore>()
                .AsSelf()
                .As<IPersistenceStore>()
                .SingleInstance();
        }
    }
}