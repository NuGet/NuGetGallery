// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace Ng.Helpers
{
    /// <summary>
    /// This type doesn't need to do anything as secret injection happens at a higher level.
    /// </summary>
    public class EmptySecretInjector : ISecretInjector
    {
        public Task<string> InjectAsync(string input)
        {
            return Task.FromResult(input);
        }

        public Task<string> InjectAsync(string input, ILogger logger)
        {
            return Task.FromResult(input);
        }
    }
}
