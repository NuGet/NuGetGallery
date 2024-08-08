// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Authentication
{
    public class CredentialTypeInfo(string type, bool isApiKey, string description)
    {
        public string Type { get; } = type;
        public bool IsApiKey { get; } = isApiKey;
        public string Description { get; } = description;
    }
}
