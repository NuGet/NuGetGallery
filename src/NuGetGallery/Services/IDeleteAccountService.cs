// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IDeleteAccountService
    {
        Task<Tuple<bool, List<string>>> DeleteGalleryUserAccountAsync(User userToBeDeleted, User admin, string signature, bool unsignOrphanPackages);
    }
}
