// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Security.KeyVault.Keys;

namespace NuGet.Services.KeyVault
{
    internal static class KeyVaultUriValidator
    {
        public static Uri CreateVaultUri(string vaultName)
        {
            if (!IsValidVaultName(vaultName))
            {
                throw new ArgumentException("The Key Vault name is invalid.", nameof(vaultName));
            }

            return new Uri($"https://{vaultName}.vault.azure.net/", UriKind.Absolute);
        }

        public static KeyVaultKeyIdentifier ValidateKeyIdentifier(string keyId, Uri expectedVaultUri)
        {
            if (keyId == null)
            {
                throw new ArgumentNullException(nameof(keyId));
            }

            if (!Uri.TryCreate(keyId, UriKind.Absolute, out Uri keyUri))
            {
                throw new ArgumentException("The key identifier must be an absolute URI.", nameof(keyId));
            }

            return ValidateKeyIdentifier(keyUri, expectedVaultUri, nameof(keyId));
        }

        public static KeyVaultKeyIdentifier ValidateKeyIdentifier(Uri keyUri, Uri expectedVaultUri)
        {
            return ValidateKeyIdentifier(keyUri, expectedVaultUri, nameof(keyUri));
        }

        private static KeyVaultKeyIdentifier ValidateKeyIdentifier(Uri keyUri, Uri expectedVaultUri, string parameterName)
        {
            if (keyUri == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (expectedVaultUri == null)
            {
                throw new ArgumentNullException(nameof(expectedVaultUri));
            }

            if (!keyUri.IsAbsoluteUri
                || !string.Equals(keyUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !keyUri.IsDefaultPort
                || !string.IsNullOrEmpty(keyUri.UserInfo)
                || !string.IsNullOrEmpty(keyUri.Query)
                || !string.IsNullOrEmpty(keyUri.Fragment)
                || !string.Equals(keyUri.Host, expectedVaultUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The key identifier must be an HTTPS URI in the configured Key Vault.", parameterName);
            }

            string[] segments = keyUri.AbsolutePath.Split('/');
            if ((segments.Length != 3 && segments.Length != 4)
                || segments[0].Length != 0
                || !string.Equals(segments[1], "keys", StringComparison.Ordinal)
                || !IsValidPathSegment(segments[2])
                || (segments.Length == 4 && !IsValidPathSegment(segments[3])))
            {
                throw new ArgumentException("The key identifier must have the path /keys/{name} or /keys/{name}/{version}.", parameterName);
            }

            if (!KeyVaultKeyIdentifier.TryCreate(keyUri, out KeyVaultKeyIdentifier identifier))
            {
                throw new ArgumentException("The key identifier is invalid.", parameterName);
            }

            return identifier;
        }

        private static bool IsValidPathSegment(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) < 0
                && value.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsValidVaultName(string vaultName)
        {
            if (string.IsNullOrWhiteSpace(vaultName)
                || vaultName.Length < 3
                || vaultName.Length > 24
                || !IsAsciiLetter(vaultName[0])
                || !IsAsciiLetterOrDigit(vaultName[vaultName.Length - 1]))
            {
                return false;
            }

            bool previousWasHyphen = false;
            for (int i = 0; i < vaultName.Length; i++)
            {
                char character = vaultName[i];
                if (character == '-')
                {
                    if (previousWasHyphen)
                    {
                        return false;
                    }

                    previousWasHyphen = true;
                }
                else
                {
                    if (!IsAsciiLetterOrDigit(character))
                    {
                        return false;
                    }

                    previousWasHyphen = false;
                }
            }

            return true;
        }

        private static bool IsAsciiLetter(char character)
        {
            return (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z');
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return IsAsciiLetter(character)
                || (character >= '0' && character <= '9');
        }
    }
}
