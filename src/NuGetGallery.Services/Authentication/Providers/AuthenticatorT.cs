// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication.Providers
{
    public abstract class Authenticator<TConfig> : Authenticator
        where TConfig : AuthenticatorConfiguration, new()
    {
        public TConfig Config { get; private set; }

        protected internal override AuthenticatorConfiguration CreateConfigObject()
        {
            Config = new TConfig();
            return Config;
        }
    }
}