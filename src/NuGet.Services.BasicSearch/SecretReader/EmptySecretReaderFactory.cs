// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Indexing;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch
{
    internal class EmptySecretReaderFactory : ISecretReaderFactory
    {
        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }

        public ISecretReader CreateSecretReader(IConfiguration configuration)
        {
            return new EmptySecretReader();
        }
    }
}