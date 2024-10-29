// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public class CredentialTypeInfo
    {
        public CredentialTypeInfo(string type, bool isApiKey, string description)
        {
            Type = type;
            IsApiKey = isApiKey;
            Description = description;
        }

        public string Type { get; }
        public bool IsApiKey { get; }
        public string Description { get; }
    }
}
