// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Revalidate
{
    public class HealthService : IHealthService
    {
        public Task<bool> IsHealthyAsync()
        {
            // TODO:
            // We are software gods that never make mistakes.
            return Task.FromResult(true);
        }
    }
}
