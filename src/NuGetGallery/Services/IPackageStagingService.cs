// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageStagingService
    {
        Task<PackageStagingResult> StagePackageAsync(User currentUser, IEnumerable<Scope> scopes, HttpContextBase httpContext, Stream packageFile);

        PackageStagingStatus GetPackage(User currentUser, IEnumerable<Scope> scopes, string id, string version);
    }
}
