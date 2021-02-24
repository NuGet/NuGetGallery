// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs.Validation
{
    public class SharedAccessSignatureService : ISharedAccessSignatureService
    {
        private readonly ISecretReader _secretReader;

        public SharedAccessSignatureService(ISecretReader secretReader)
        {
            _secretReader = secretReader ?? throw new ArgumentNullException(nameof(secretReader));
        }

        public async Task<string> GetFromManagedStorageAccountAsync(string sasDefinition)
        {
            return await _secretReader.GetSecretAsync(sasDefinition);
        }
    }
}
