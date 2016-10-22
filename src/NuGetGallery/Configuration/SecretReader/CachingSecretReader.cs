// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Configuration.SecretReader
{
    public class CachingSecretReader : ISecretReader
    {
        private ISecretReader _internalReader;
        private Dictionary<string, string> _cache;
        private IDiagnosticsSource _trace;

        public CachingSecretReader(ISecretReader secretReader, IDiagnosticsService diagnosticsService)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }

            _internalReader = secretReader;
            _cache = new Dictionary<string, string>();
            _trace = diagnosticsService.GetSource("CachingSecretReader");
        }
            
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (!_cache.ContainsKey(secretName))
            {
                _trace.Information("Cache miss for setting " + secretName);
                var secretValue = await _internalReader.GetSecretAsync(secretName);
                _cache[secretName] = secretValue;
            }

            return _cache[secretName];
        }
    }
}