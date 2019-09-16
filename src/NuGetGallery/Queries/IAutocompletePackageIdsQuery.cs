// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public interface IAutocompletePackageIdsQuery
    {
        Task<IReadOnlyList<string>> Execute(
            string partialId,
            bool? includePrerelease = false,
            string semVerLevel = null);
    }
}