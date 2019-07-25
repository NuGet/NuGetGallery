// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public interface ISecretReader
    {
        Task<string> GetSecretAsync(string secretName);
        Task<ISecret> GetSecretObjectAsync(string secretName);
    }
}