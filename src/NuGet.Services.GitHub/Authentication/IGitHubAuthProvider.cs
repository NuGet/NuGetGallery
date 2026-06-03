// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.GitHub.Authentication
{
    public interface IGitHubAuthProvider
    {
        public Task AddAuthentication(HttpRequestMessage message);
    }
}
