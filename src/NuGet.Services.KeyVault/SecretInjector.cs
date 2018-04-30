// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public class SecretInjector : ISecretInjector
    {
        public const string DefaultFrame = "$$";
        private readonly string _frame;

        public ISecretReader SecretReader { get; }

        public SecretInjector(ISecretReader secretReader) : this(secretReader, DefaultFrame)
        {
        }

        public SecretInjector(ISecretReader secretReader, string frame)
        {
            SecretReader = secretReader ?? throw new ArgumentNullException(nameof(SecretReader));

            if (string.IsNullOrWhiteSpace(frame))
            {
                throw new ArgumentException("Frame argument is null or empty.");
            }

            _frame = frame;
        }

        public async Task<string> InjectAsync(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            var output = new StringBuilder(input);
            var secretNames = GetSecretNames(input);

            foreach (var secretName in secretNames)
            {
                var secretValue = await SecretReader.GetSecretAsync(secretName);
                output.Replace($"{_frame}{secretName}{_frame}", secretValue);
            }

            return output.ToString();
        }

        public IEnumerable<string> GetSecretNames(string input)
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