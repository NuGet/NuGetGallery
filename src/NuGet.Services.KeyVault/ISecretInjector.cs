// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.KeyVault
{
    public interface ISecretInjector
    {
        string Inject(string input);
        string Inject(string input, ILogger logger);
        Task<string> InjectAsync(string input);
        Task<string> InjectAsync(string input, ILogger logger);
    }
}
