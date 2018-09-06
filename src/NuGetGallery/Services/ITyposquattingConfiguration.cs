// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public interface ITyposquattingConfiguration
    {
        int PackageIdChecklistLength { get; }
        bool IsCheckEnabled { get; }
        bool IsBlockUsersEnabled { get; }
    }
}