// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Threading.Tasks;
using NuGet.Protocol.Registration;

namespace NuGet.Services
{
    public class InMemoryRegistrationClient : IRegistrationClient
    {
        public ConcurrentDictionary<string, RegistrationIndex> Indexes { get; } = new ConcurrentDictionary<string, RegistrationIndex>();
        public ConcurrentDictionary<string, RegistrationPage> Pages { get; } = new ConcurrentDictionary<string, RegistrationPage>();
        public ConcurrentDictionary<string, RegistrationLeaf> Leaves { get; } = new ConcurrentDictionary<string, RegistrationLeaf>();

        public Task<RegistrationIndex> GetIndexOrNullAsync(string indexUrl)
        {
            if (Indexes.TryGetValue(indexUrl, out var index))
            {
                return Task.FromResult(index);
            }

            return Task.FromResult<RegistrationIndex>(null);
        }

        public Task<RegistrationLeaf> GetLeafAsync(string leafUrl)
        {
            return Task.FromResult(Leaves[leafUrl]);
        }

        public Task<RegistrationPage> GetPageAsync(string pageUrl)
        {
            return Task.FromResult(Pages[pageUrl]);
        }
    }
}
