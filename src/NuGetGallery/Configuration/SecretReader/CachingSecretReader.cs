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
        public const int DaysToInvalidate = 1;

        private ISecretReader _internalReader;
        private Dictionary<string, Tuple<string, DateTime>> _cache;
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
            _cache = new Dictionary<string, Tuple<string, DateTime>>();
            _trace = diagnosticsService.GetSource("CachingSecretReader");
        }
            
        public async Task<string> GetSecretAsync(string secretName)
        {
            if (!_cache.ContainsKey(secretName) || _cache[secretName].Item2.Subtract(DateTime.Now).Days > DaysToInvalidate)
            {
                _trace.Information("Cache miss for setting " + secretName);
                var secretValue = await _internalReader.GetSecretAsync(secretName);
                _cache[secretName] = Tuple.Create<string, DateTime>(secretValue, DateTime.Now);
            }

            return _cache[secretName].Item1;
        }
    }
}