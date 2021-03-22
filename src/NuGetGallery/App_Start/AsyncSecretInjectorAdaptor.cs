// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace NuGetGallery
{
    public class AsyncSecretInjectorAdaptor : ISecretInjector
    {
        private readonly ISyncSecretInjector _underlyingInjector;

        public AsyncSecretInjectorAdaptor(ISyncSecretInjector underlyingInjector)
        {
            _underlyingInjector = underlyingInjector ?? throw new ArgumentNullException(nameof(underlyingInjector));
        }

        public Task<string> InjectAsync(string input)
            => Task.FromResult(_underlyingInjector.Inject(input));

        public Task<string> InjectAsync(string input, ILogger logger)
            => Task.FromResult(_underlyingInjector.Inject(input));
    }
}