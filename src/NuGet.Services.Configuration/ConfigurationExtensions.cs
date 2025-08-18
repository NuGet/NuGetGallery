// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace NuGet.Services.Configuration
{
    public static class ConfigurationExtensions
    {
        public static TokenCredential GetTokenCredential(this IConfiguration configuration)
        {
            string clientId = configuration[Constants.ManagedIdentityClientIdKey];

#if DEBUG
            return new DefaultAzureCredential();
#else
            return new ManagedIdentityCredential(clientId);
#endif
        }
    }
}
