// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery
{
    /// <summary>
    /// Determines whether a cloud storage object (e.g. a blob container) is available. The purpose of this interface
    /// is to assess a gallery instance's connection to Azure Storage.
    /// </summary>
    public interface ICloudStorageStatusDependency
    {
        Task<bool> IsAvailableAsync(CloudBlobLocationMode? locationMode);
    }
}