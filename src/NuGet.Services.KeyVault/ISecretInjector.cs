// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public interface ISecretInjector
    {
        string GetSecretName(string input);

        Task<string> InjectAsync(string input);

        Task<string> InjectAsync(string input, bool forceRefresh);
    }
}