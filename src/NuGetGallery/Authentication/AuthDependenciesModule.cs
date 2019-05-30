// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using NuGetGallery.Authentication.Providers;

namespace NuGetGallery.Authentication
{
    public class AuthDependenciesModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AuthenticationService>()
                .As<AuthenticationService>()
                .InstancePerLifetimeScope();

            builder.RegisterType<AuthenticationService>()
                .As<IAuthenticationService>()
                .InstancePerLifetimeScope();

            foreach (var instance in Authenticator.GetAllAvailable())
            {
                builder.RegisterInstance(instance)
                    .As<Authenticator>()
                    .SingleInstance();
            }
        }
    }
}