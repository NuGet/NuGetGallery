// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    public interface IVerifiedPackagesDataClient
    {
        Task<AuxiliaryFileResult<HashSet<string>>> ReadLatestAsync(IAccessCondition accessCondition, StringCache stringCache);
        Task ReplaceLatestAsync(HashSet<string> newData, IAccessCondition accessCondition);
    }
}