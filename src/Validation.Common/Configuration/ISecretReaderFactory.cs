// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using NuGet.Services.KeyVault;

namespace NuGet.Jobs.Validation.Common
{
    public interface ISecretReaderFactory
    {
        ISecretInjector CreateSecretInjector(ISecretReader secretReader);

        ISecretReader CreateSecretReader(IConfigurationService configurationService);
    }
}
