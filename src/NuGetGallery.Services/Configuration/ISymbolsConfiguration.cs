// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;

namespace NuGetGallery.Services
{
    public interface ISymbolsConfiguration
    {
        bool IsSymbolsUploadEnabledForUser(User user);
    }
}