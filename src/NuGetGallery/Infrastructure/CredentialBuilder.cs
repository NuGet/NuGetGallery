using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery
{
    /// <summary>
    /// Provides helper methods to generate credentials.
    /// </summary>
    public static class CredentialBuilder
    {
        public static Credential CreateV1ApiKey()
        {
            return CreateV1ApiKey(Guid.NewGuid());
        }

        public static Credential CreateV1ApiKey(Guid apiKey)
        {
            var value = apiKey
                .ToString()
                .ToLowerInvariant();
            return new Credential(Constants.CredentialTypes.ApiKeyV1, value);
        }
    }
}