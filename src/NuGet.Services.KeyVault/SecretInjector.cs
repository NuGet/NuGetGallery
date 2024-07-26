// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.KeyVault
{
    public class SecretInjector : ICachingSecretInjector
    {
        public const string DefaultFrame = "$$";
        private readonly string _frame;
        private readonly ISecretReader _secretReader;
        private readonly ICachingSecretReader _cachingSecretReader = null;

        public SecretInjector(ISecretReader secretReader) : this(secretReader, DefaultFrame)
        {
        }

        public SecretInjector(ISecretReader secretReader, string frame)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            if (string.IsNullOrWhiteSpace(frame))
            {
                throw new ArgumentException("Frame argument is null or empty.");
            }

            _frame = frame;
            _secretReader = secretReader;
            _cachingSecretReader = secretReader as ICachingSecretReader;
        }

        public Task<string> InjectAsync(string input)
        {
            return InjectAsync(input, logger: null);
        }

        public async Task<string> InjectAsync(string input, ILogger logger)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var output = new StringBuilder(input);
            var secretNames = GetSecretNames(input);

            foreach (var secretName in secretNames)
            {
                var secretValue = await _secretReader.GetSecretAsync(secretName, logger);
                output.Replace($"{_frame}{secretName}{_frame}", secretValue);
            }

            return output.ToString();
        }

        public bool TryInjectCached(string input, out string injectedString) => TryInjectCached(input, logger: null, out injectedString);

        public bool TryInjectCached(string input, ILogger logger, out string injectedString)
        {
            if (string.IsNullOrEmpty(input))
            {
                injectedString = input;
                return true;
            }

            var output = new StringBuilder(input);
            var secretNames = GetSecretNames(input);
            injectedString = null;

            if (secretNames.Count > 0 && _cachingSecretReader == null)
            {
                // we have secrets to inject, but no caching secret reader to read them from
                return false;
            }

            foreach (var secretName in secretNames)
            {
                string secretValue = null;
                if (_cachingSecretReader?.TryGetCachedSecret(secretName, logger, out secretValue) != true)
                {
                    // current secret is not available in cache or no caching secret reader
                    return false;
                }
                output.Replace($"{_frame}{secretName}{_frame}", secretValue);
            }

            injectedString = output.ToString();
            return true;
        }

        private ICollection<string> GetSecretNames(string input)
        {
            var secretNames = new HashSet<string>();

            int startIndex = 0;
            int foundIndex;
            bool insideFrame = false;

            do
            {
                foundIndex = input.IndexOf(_frame, startIndex, StringComparison.InvariantCulture);

                if (insideFrame && foundIndex > 0)
                {
                    var secret = input.Substring(startIndex, foundIndex - startIndex);
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        secretNames.Add(secret);
                    }

                    insideFrame = false;
                }
                else
                {
                    insideFrame = true;
                }

                startIndex = foundIndex + _frame.Length;

            } while (foundIndex >= 0);

            return secretNames;
        }
    }
}