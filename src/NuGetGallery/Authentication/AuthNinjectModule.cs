// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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