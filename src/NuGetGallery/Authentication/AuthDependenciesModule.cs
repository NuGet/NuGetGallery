// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.AspNetCore.DataProtection;
using NuGetGallery.Authentication.Providers;
using NuGetGallery.Authentication.Providers.Cookie;

namespace NuGetGallery.Authentication
{
    public class AuthDependenciesModule
        : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<AuthenticationService>()
                .As<AuthenticationService>()
                .As<IAuthenticationService>()
                .InstancePerLifetimeScope();

            foreach (var instance in Authenticator.GetAllAvailable())
            {
                if (instance is LocalUserAuthenticator)
                {
                    builder.Register(context => new LocalUserAuthenticator(
                            context.Resolve<IDataProtectionProvider>()))
                        .As<Authenticator>()
                        .SingleInstance();
                }
                else
                {
                    builder.RegisterInstance(instance)
                        .As<Authenticator>()
                        .SingleInstance();
                }
            }
        }
    }
}