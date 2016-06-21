// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.KeyVault
{
    public static class SecretReaderFormatter
    {
        private const string Frame = "$$";

        public static string Format(this ISecretReader secretReader, string input)
        {
            return FormatAsync(secretReader, input).Result;
        }

        public static async Task<string> FormatAsync(this ISecretReader secretReader, string input)
        {
            var output = new StringBuilder(input);
            var secretNames = GetSecretNames(input);

            foreach (var secretName in secretNames)
            {
                var secretValue = await secretReader.ReadSecretAsync(secretName);
                output.Replace($"{Frame}{secretName}{Frame}", secretValue);
            }

            return output.ToString();
        }

        private static IEnumerable<string> GetSecretNames(string input)
        {
            var secretNames = new HashSet<string>();

            int startIndex = 0;
            int foundIndex;
            bool insideFrame = false;

            do
            {
                foundIndex = input.IndexOf(Frame, startIndex, StringComparison.InvariantCulture);

                if (insideFrame && foundIndex > 0)
                {
                    var secret = input.Substring(startIndex, foundIndex - startIndex).Replace(Frame, string.Empty);
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

                startIndex = foundIndex + Frame.Length;

            } while (foundIndex >= 0);

            return secretNames;
        }
    }
}
