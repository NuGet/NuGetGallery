// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public static class KeyVaultConfigurationExtensions
    {
        public static Uri GetKeyVaultUri(this KeyVaultConfiguration self)
        {
            var uriString = $"https://{self.VaultName}.vault.azure.net/";
            return new Uri(uriString);
        }
    }
}
