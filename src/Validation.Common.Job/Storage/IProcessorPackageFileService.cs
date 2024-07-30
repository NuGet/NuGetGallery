// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Storage
{
    public interface IProcessorPackageFileService
    {
        Task<Uri> GetReadAndDeleteUriAsync(string packageId, string packageNormalizedVersion, Guid validationId, string sasDefinition);
        Task SaveAsync(string packageId, string packageNormalizedVersion, Guid validationId, Stream packageFile);
    }
}