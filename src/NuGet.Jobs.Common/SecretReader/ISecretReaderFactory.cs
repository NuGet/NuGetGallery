// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs
{
    public interface ISecretReaderFactory
    {
        ICachingSecretReader CreateSecretReader(IDictionary<string, string> settings);

        ICachingSecretInjector CreateSecretInjector(ISecretReader secretReader);
    }
}
