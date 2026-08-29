// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public interface IPackageStagingAuthorizationService
    {
        bool CanManage(User currentUser, StagedPackage stagedPackage);

        bool CanManageWithApiKey(User currentUser, IEnumerable<Scope> scopes, StagedPackage stagedPackage);
    }
}
