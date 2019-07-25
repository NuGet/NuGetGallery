// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class EmptySecretReader : ISecretReader
    {
        public Task<string> GetSecretAsync(string secretName)
        {
            return Task.FromResult(secretName);
        }        

        public Task<ISecret> GetSecretObjectAsync(string secretName)
        {            
            return Task.FromResult((ISecret)new KeyVaultSecret(secretName, secretName, null));
        }        
    }
}