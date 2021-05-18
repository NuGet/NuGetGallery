// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.KeyVault;

namespace NuGetGallery.Configuration.SecretReader
{
    public class EmptySecretReaderFactory
    {
        public ISyncSecretInjector CreateSecretInjector(ISyncSecretReader secretReader)
        {
            return new SyncSecretInjector(secretReader);
        }

        public ISyncSecretReader CreateSecretReader()
        {
            return new EmptySyncSecretReader();
        }
    }
}